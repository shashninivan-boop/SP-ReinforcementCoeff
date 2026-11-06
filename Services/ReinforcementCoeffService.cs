using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using SP_Addin.ReinforcementCoeff.Models;

namespace SP_Addin.ReinforcementCoeff.Services
{
    /// <summary>Итог расчёта по всем обработанным группам.</summary>
    public sealed class ReinforcementCoeffRunResult
    {
        public IReadOnlyList<ReinforcementCoeffGroupResult> Groups { get; set; } =
            Array.Empty<ReinforcementCoeffGroupResult>();

        public string SummaryText { get; set; } = string.Empty;

        /// <summary>Значения для записи в модель; заполняется после расчёта без транзакции.</summary>
        public IReadOnlyList<ReinforcementCoeffParameterWrite> PendingWrites { get; set; } =
            Array.Empty<ReinforcementCoeffParameterWrite>();
    }

    /// <summary>Результат записи подготовленных значений в параметры модели.</summary>
    public sealed class ReinforcementCoeffWriteResult
    {
        public int WrittenCount { get; set; }
        public int SkippedCount { get; set; }
        public string SummaryText { get; set; } = string.Empty;
    }

    /// <summary>
    /// Расчёт коэффициента армирования и опциональная запись параметров в модель.
    /// Логика перенесена из Dynamo-скрипта «Наработки скрипта по подсчету Коэф арм».
    /// </summary>
    public sealed class ReinforcementCoeffService
    {
        public ReinforcementCoeffRunResult Run(Document doc, ReinforcementCoeffContext context)
        {
            if (doc == null)
            {
                throw new ArgumentNullException(nameof(doc));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            string kitName = context.KitName?.Trim() ?? string.Empty;

            // Коллекторы и параметры элементов читаем один раз: каждый элемент
            // по паре (комплект, ключ группировки) попадает максимум в одну группу.
            IReadOnlyList<ElementGroupEntry> concreteEntries = CollectConcreteEntries(doc, context, kitName);
            IReadOnlyList<ElementGroupEntry> rebarEntries = CollectRebarEntries(doc, context, kitName);

            IReadOnlyList<GroupDefinition> groups = BuildGroupDefinitions(doc, context, concreteEntries, rebarEntries);
            if (groups.Count == 0)
            {
                return new ReinforcementCoeffRunResult
                {
                    SummaryText = context.GroupingMode == ReinforcementCoeffGroupingMode.ByElevation
                        ? string.IsNullOrEmpty(kitName)
                            ? "Не найдено групп для расчёта по отметке."
                            : "В модели не найдено групп для расчёта по отметке в выбранном комплекте."
                        : string.IsNullOrEmpty(kitName)
                            ? "Не найдено групп для расчёта по параметру группировки."
                            : "Не найдено групп для расчёта по параметру группировки в выбранном комплекте.",
                };
            }

            Dictionary<(string GroupingValue, string KitName), List<Element>> concreteMembersByGroup =
                BuildMembersMap(concreteEntries);
            Dictionary<(string GroupingValue, string KitName), List<Element>> rebarMembersByGroup =
                BuildMembersMap(rebarEntries);

            var results = new List<ReinforcementCoeffGroupResult>();
            var pendingWrites = new List<ReinforcementCoeffParameterWrite>();

            foreach (GroupDefinition group in groups)
            {
                var groupKey = (group.GroupingKey, group.KitName);
                GroupProcessResult groupResult = ProcessGroup(
                    doc,
                    group,
                    GetGroupMembers(concreteMembersByGroup, groupKey),
                    GetGroupMembers(rebarMembersByGroup, groupKey));
                results.Add(groupResult.Result);
                pendingWrites.AddRange(groupResult.PendingWrites);
            }

            int successCount = results.Count(r => r.IsSuccess);
            return new ReinforcementCoeffRunResult
            {
                Groups = results,
                PendingWrites = pendingWrites,
                SummaryText = $"Рассчитано групп: {successCount} из {results.Count}. Запись в параметры не выполнялась.",
            };
        }

        public ReinforcementCoeffWriteResult WriteParameters(
            Document doc,
            IReadOnlyList<ReinforcementCoeffParameterWrite> writes)
        {
            if (doc == null)
            {
                throw new ArgumentNullException(nameof(doc));
            }

            if (writes == null || writes.Count == 0)
            {
                return new ReinforcementCoeffWriteResult
                {
                    SummaryText = "Нет данных для записи. Сначала выполните расчёт.",
                };
            }

            int writtenCount = 0;
            int skippedCount = 0;

            using (Transaction transaction = new Transaction(doc, ReinforcementCoeffConstants.TransactionName))
            {
                transaction.Start();

                foreach (ReinforcementCoeffParameterWrite write in writes)
                {
                    Element? element = doc.GetElement(write.ElementId);
                    if (element == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    Parameter? parameter = ReinforcementCoeffParameterHelper.FindParameter(
                        element,
                        write.ParameterName);
                    if (ReinforcementCoeffParameterHelper.TrySetDoubleValue(parameter, write.Value))
                    {
                        writtenCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }

                transaction.Commit();
            }

            return new ReinforcementCoeffWriteResult
            {
                WrittenCount = writtenCount,
                SkippedCount = skippedCount,
                SummaryText = $"Записано значений: {writtenCount}. Пропущено: {skippedCount}.",
            };
        }

        /// <summary>
        /// Расчёт одной группы: сумма объёмов бетона, сумма масс арматуры,
        /// подготовка записей SP_Масса арматуры на элемент и SP_Средний расход арматуры.
        /// </summary>
        private static GroupProcessResult ProcessGroup(
            Document doc,
            GroupDefinition group,
            IReadOnlyList<Element> groupConcreteElements,
            IReadOnlyList<Element> groupRebarElements)
        {
            var result = new ReinforcementCoeffGroupResult
            {
                KitName = group.KitName,
                GroupingDisplay = group.GroupingDisplay,
            };
            var pendingWrites = new List<ReinforcementCoeffParameterWrite>();

            var concreteVolumes = new List<(Element Element, double Volume)>();
            foreach (Element element in groupConcreteElements)
            {
                if (ReinforcementCoeffConcreteVolumeHelper.TryGetConcreteVolumeCubicMeters(
                        doc,
                        element,
                        out double volume))
                {
                    concreteVolumes.Add((element, volume));
                }
            }

            List<Element> concreteElements = concreteVolumes.Select(pair => pair.Element).ToList();

            List<Element> rebarElements = groupRebarElements.Where(e => !IsFrameElement(e)).ToList();

            if (string.IsNullOrEmpty(result.GroupingDisplay))
            {
                // Только режим по отметке: исходный текст ADSK_Группирование / RNF.Марка конструкции для колонки отчёта.
                Element? sample = concreteElements.FirstOrDefault() ?? rebarElements.FirstOrDefault();
                if (sample != null)
                {
                    result.GroupingDisplay = ElevationGroupingHelper.GetElementGroupingDisplayText(sample);
                }
            }

            result.ConcreteElementCount = concreteElements.Count;
            result.RebarElementCount = rebarElements.Count;
            result.ConcreteElementIds = ToElementIdList(concreteElements);
            result.RebarElementIds = ToElementIdList(rebarElements);
            result.ConcreteContributions = BuildConcreteContributions(concreteVolumes);

            if (concreteElements.Count == 0)
            {
                result.Status = "Не найдены бетонные элементы группы.";
                return new GroupProcessResult(result, pendingWrites);
            }

            // Знаменатель — сумма значений «Объем» выбранных элементов в м³, как в графе Dynamo.
            double totalVolume = concreteVolumes.Sum(pair => pair.Volume);

            result.ConcreteVolume = totalVolume;
            if (totalVolume <= 0)
            {
                result.Status = "Суммарный объём выбранных элементов равен нулю.";
                return new GroupProcessResult(result, pendingWrites);
            }

            // Числитель — сумма масс стержней; значения для записи собираем отдельно.
            double totalRebarMass = 0;
            var rebarContributions = new List<ReinforcementCoeffElementContribution>();
            foreach (Element element in rebarElements)
            {
                if (!TryCalculateRebarMass(element, out double massPerElement))
                {
                    continue;
                }

                totalRebarMass += massPerElement;
                rebarContributions.Add(CreateRebarContribution(doc, element, massPerElement));
                pendingWrites.Add(new ReinforcementCoeffParameterWrite
                {
                    ElementId = element.Id,
                    ParameterName = ReinforcementCoeffConstants.ParamMassPerElement,
                    Value = massPerElement,
                });
            }

            result.RebarContributions = rebarContributions
                .OrderByDescending(item => item.Contribution)
                .ToList();
            result.RebarMass = totalRebarMass;
            double coefficient = totalRebarMass / totalVolume;
            result.Coefficient = coefficient;

            foreach (Element element in concreteElements)
            {
                pendingWrites.Add(new ReinforcementCoeffParameterWrite
                {
                    ElementId = element.Id,
                    ParameterName = ReinforcementCoeffConstants.ParamAvgConsumption,
                    Value = coefficient,
                });
            }

            result.IsSuccess = true;
            result.Status = "OK";
            return new GroupProcessResult(result, pendingWrites);
        }

        private sealed class GroupProcessResult
        {
            public GroupProcessResult(
                ReinforcementCoeffGroupResult result,
                IReadOnlyList<ReinforcementCoeffParameterWrite> pendingWrites)
            {
                Result = result;
                PendingWrites = pendingWrites;
            }

            public ReinforcementCoeffGroupResult Result { get; }
            public IReadOnlyList<ReinforcementCoeffParameterWrite> PendingWrites { get; }
        }

        private static List<long> ToElementIdList(IEnumerable<Element> elements) =>
            elements
                .Select(e => e.Id.GetValue())
                .Where(id => id > 0)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

        /// <summary>Собирает состав бетона группы по убыванию объёма.</summary>
        private static List<ReinforcementCoeffElementContribution> BuildConcreteContributions(
            IReadOnlyList<(Element Element, double Volume)> concreteVolumes)
        {
            return concreteVolumes
                .Select(pair => new ReinforcementCoeffElementContribution
                {
                    ElementId = pair.Element.Id.GetValue(),
                    DisplayName = GetElementDisplayName(pair.Element),
                    Contribution = pair.Volume,
                    Kind = ReinforcementCoeffContributionKind.Concrete,
                    OwnerViewId = 0,
                    OwnerViewName = "Модель",
                })
                .OrderByDescending(item => item.Contribution)
                .ToList();
        }

        /// <summary>Создаёт вклад арматурного узла с owner-view для навигации.</summary>
        private static ReinforcementCoeffElementContribution CreateRebarContribution(
            Document doc,
            Element element,
            double massPerElement)
        {
            ResolveOwnerView(doc, element, out long ownerViewId, out string ownerViewName);
            return new ReinforcementCoeffElementContribution
            {
                ElementId = element.Id.GetValue(),
                DisplayName = GetElementDisplayName(element),
                Contribution = massPerElement,
                Kind = ReinforcementCoeffContributionKind.Rebar,
                OwnerViewId = ownerViewId,
                OwnerViewName = ownerViewName,
            };
        }

        /// <summary>Имя для списка состава: семейство : тип, иначе Name/категория.</summary>
        private static string GetElementDisplayName(Element element)
        {
            if (element is FamilyInstance familyInstance)
            {
                FamilySymbol? symbol = familyInstance.Symbol;
                if (symbol != null)
                {
                    string familyName = symbol.FamilyName?.Trim() ?? string.Empty;
                    string typeName = symbol.Name?.Trim() ?? string.Empty;
                    if (!string.IsNullOrEmpty(familyName) && !string.IsNullOrEmpty(typeName))
                    {
                        return familyName + " : " + typeName;
                    }

                    if (!string.IsNullOrEmpty(typeName))
                    {
                        return typeName;
                    }

                    if (!string.IsNullOrEmpty(familyName))
                    {
                        return familyName;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(element.Name))
            {
                return element.Name.Trim();
            }

            if (element.Category != null && !string.IsNullOrWhiteSpace(element.Category.Name))
            {
                return element.Category.Name;
            }

            return "Id " + element.Id.GetValue().ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Читает owner-view узла для перехода с панели состава.</summary>
        private static void ResolveOwnerView(
            Document doc,
            Element element,
            out long ownerViewId,
            out string ownerViewName)
        {
            ownerViewId = 0;
            ownerViewName = "(без вида)";

            try
            {
                ElementId viewId = element.OwnerViewId;
                if (viewId == null || viewId == ElementId.InvalidElementId)
                {
                    return;
                }

                ownerViewId = viewId.GetValue();
                if (doc.GetElement(viewId) is View view && !string.IsNullOrWhiteSpace(view.Name))
                {
                    ownerViewName = view.Name.Trim();
                }
                else
                {
                    ownerViewName = "Вид " + ownerViewId.ToString(CultureInfo.InvariantCulture);
                }
            }
            catch (Exception)
            {
                ownerViewId = 0;
                ownerViewName = "(без вида)";
            }
        }

        /// <summary>
        /// Формула из спецификации RNF (узел Dynamo «if RNF.Тип массы = 1, ...»):
        /// масса = RNF.Масса элемента × qty × RNF.Количество сборок_число,
        /// где qty = Кол-во × Длина пм при типе массы 1, иначе только Кол-во.
        /// </summary>
        private static bool TryCalculateRebarMass(Element element, out double massPerElement)
        {
            massPerElement = 0;
            if (!ReinforcementCoeffParameterHelper.TryReadDoubleValue(element,
                    ReinforcementCoeffConstants.ParamRebarMass, out double barMass) ||
                !ReinforcementCoeffParameterHelper.TryReadIntValue(element,
                    ReinforcementCoeffConstants.ParamElementCount, out int elementCount) ||
                !ReinforcementCoeffParameterHelper.TryReadDoubleValue(element,
                    ReinforcementCoeffConstants.ParamAssemblyCount, out double assemblies))
                return false;

            // В графе RNF.Тип массы читается через Element.ElementType.
            Element? type = element.Document.GetElement(element.GetTypeId());
            if (type == null || !ReinforcementCoeffParameterHelper.TryReadIntValue(type,
                    ReinforcementCoeffConstants.ParamMassType, out int massType))
            {
                if (!ReinforcementCoeffParameterHelper.TryReadIntValue(element,
                        ReinforcementCoeffConstants.ParamMassType, out massType))
                    return false;
            }
            double? length = null;
            if (ReinforcementCoeffParameterHelper.TryReadDoubleValue(element,
                    ReinforcementCoeffConstants.ParamLengthPm, out double lengthValue))
            {
                Parameter? lengthParameter = ReinforcementCoeffParameterHelper.FindParameter(element,
                    ReinforcementCoeffConstants.ParamLengthPm);
                length = lengthParameter?.StorageType == StorageType.Double &&
                    lengthParameter.Definition.GetDataType() == SpecTypeId.Length
                        ? UnitUtils.ConvertFromInternalUnits(lengthValue, UnitTypeId.Meters) : lengthValue;
            }
            Parameter? massParameter = ReinforcementCoeffParameterHelper.FindParameter(element,
                ReinforcementCoeffConstants.ParamRebarMass);
            if (massParameter?.StorageType == StorageType.Double &&
                massParameter.Definition.GetDataType() == SpecTypeId.MassPerUnitLength)
                barMass = UnitUtils.ConvertFromInternalUnits(barMass, UnitTypeId.KilogramsPerMeter);

            massPerElement = DynamoCalculation.CalculateMass(barMass, elementCount, massType, length, assemblies);
            return !double.IsNaN(massPerElement) && !double.IsInfinity(massPerElement);
        }

        /// <summary>
        /// Формирует список групп для расчёта:
        /// режим 1 — у бетона и узлов одно значение параметра группировки (по умолчанию SP_ВРС_Группирование);
        /// режим 2 — отметка из ADSK_Группирование (бетон) и RNF.Марка конструкции (узлы).
        /// </summary>
        private static IReadOnlyList<GroupDefinition> BuildGroupDefinitions(
            Document doc,
            ReinforcementCoeffContext context,
            IReadOnlyList<ElementGroupEntry> concreteEntries,
            IReadOnlyList<ElementGroupEntry> rebarEntries)
        {
            if (context.GroupingMode == ReinforcementCoeffGroupingMode.ByGroupingValue)
            {
                string parameterName = context.GroupingParameterName?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(parameterName))
                {
                    return Array.Empty<GroupDefinition>();
                }

                var concreteBackedPairs = new HashSet<(string GroupingValue, string KitName)>(
                    GroupingValueKitPairComparer.Instance);

                foreach (ElementGroupEntry entry in concreteEntries)
                {
                    if (!ReinforcementCoeffConcreteVolumeHelper.IsQualifyingConcreteElement(doc, entry.Element))
                    {
                        continue;
                    }

                    concreteBackedPairs.Add((entry.GroupingKey, entry.Kit));
                }

                if (concreteBackedPairs.Count == 0)
                {
                    return Array.Empty<GroupDefinition>();
                }

                return concreteBackedPairs
                    .OrderBy(pair => pair.GroupingValue, StringComparer.OrdinalIgnoreCase)
                    .Select(pair => GroupDefinition.ForParameterGrouping(pair.GroupingValue, pair.KitName))
                    .ToList();
            }

            var elevationPairs = new HashSet<(string ElevationKey, string KitName)>(
                ElevationKitPairComparer.Instance);

            foreach (ElementGroupEntry entry in concreteEntries)
            {
                if (!ReinforcementCoeffConcreteVolumeHelper.IsQualifyingConcreteElement(doc, entry.Element))
                {
                    continue;
                }

                elevationPairs.Add((entry.GroupingKey, entry.Kit));
            }

            foreach (ElementGroupEntry entry in rebarEntries)
            {
                elevationPairs.Add((entry.GroupingKey, entry.Kit));
            }

            if (elevationPairs.Count == 0)
            {
                return Array.Empty<GroupDefinition>();
            }

            return elevationPairs
                .OrderBy(pair => pair.ElevationKey, StringComparer.OrdinalIgnoreCase)
                .ThenBy(pair => pair.KitName, StringComparer.OrdinalIgnoreCase)
                .Select(pair => GroupDefinition.ForElevationAndKit(pair.ElevationKey, pair.KitName))
                .ToList();
        }

        private static string ReadConcreteKit(Element element) =>
            ReadKitValue(element, ReinforcementCoeffConstants.ParamKit);

        private static string ReadRebarKit(Element element) =>
            ReadKitValue(element, ReinforcementCoeffConstants.ParamRebarSection);

        private static string ReadKitValue(Element element, string parameterName) =>
            ReinforcementCoeffParameterHelper.ReadDisplayTextValue(element, parameterName)?.Trim() ??
            ReinforcementCoeffParameterHelper.ReadStringValue(element, parameterName)?.Trim() ??
            string.Empty;

        private static string ReadGroupingParameterValue(Element element, string parameterName)
        {
            // Режим по параметру: бетон и узлы читают один и тот же параметр (например, SP_ВРС_Группирование).
            string? value = ReinforcementCoeffParameterHelper.ReadStringValue(element, parameterName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }

            return ReinforcementCoeffParameterHelper.ReadDisplayTextValue(element, parameterName)?.Trim() ??
                   string.Empty;
        }

        private static bool TryResolveConcreteKit(Element element, string kitFilter, out string kit)
        {
            kit = ReadConcreteKit(element);
            if (string.IsNullOrEmpty(kit))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(kitFilter) &&
                !string.Equals(kit, kitFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static bool TryResolveRebarKit(Element element, string kitFilter, out string kit)
        {
            kit = ReadRebarKit(element);
            if (string.IsNullOrEmpty(kit))
            {
                return false;
            }

            if (!MatchesRebarBaseFilters(element))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(kitFilter) &&
                !string.Equals(kit, kitFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static IEnumerable<Element> CollectConcreteElements(Document doc)
        {
            var elements = new List<Element>();
            foreach (BuiltInCategory category in ReinforcementCoeffConstants.ConcreteCategories)
            {
                elements.AddRange(
                    new FilteredElementCollector(doc)
                        .OfCategory(category)
                        .WhereElementIsNotElementType()
                        .ToElements());
            }

            return elements.Distinct(new ElementIdComparer());
        }

        private static IEnumerable<Element> CollectRebarElements(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfCategory(ReinforcementCoeffConstants.RebarCategory)
                .WhereElementIsNotElementType()
                .ToElements();
        }

        /// <summary>
        /// Один проход по бетону: комплект и ключ группировки читаются один раз на элемент.
        /// Пустой комплект или пустой ключ не могут попасть ни в одну группу — пропускаем.
        /// </summary>
        private static List<ElementGroupEntry> CollectConcreteEntries(
            Document doc,
            ReinforcementCoeffContext context,
            string kitFilter)
        {
            bool byGroupingValue = context.GroupingMode == ReinforcementCoeffGroupingMode.ByGroupingValue;
            string parameterName = context.GroupingParameterName?.Trim() ?? string.Empty;

            var entries = new List<ElementGroupEntry>();
            foreach (Element element in CollectConcreteElements(doc))
            {
                if (!TryResolveConcreteKit(element, kitFilter, out string kit))
                {
                    continue;
                }

                if (!MatchesAdditionalFilters(
                        element,
                        context.AdditionalFilters,
                        ReinforcementCoeffFilterTarget.Concrete))
                {
                    continue;
                }

                string groupingKey = byGroupingValue
                    ? ReadGroupingParameterValue(element, parameterName)
                    : ElevationGroupingHelper.GetElementElevationGroupKey(element);
                if (string.IsNullOrEmpty(groupingKey))
                {
                    continue;
                }

                entries.Add(new ElementGroupEntry(element, kit, groupingKey));
            }

            return entries;
        }

        /// <summary>
        /// Один проход по арматуре: базовые фильтры, комплект и ключ группировки читаются один раз.
        /// </summary>
        private static List<ElementGroupEntry> CollectRebarEntries(
            Document doc,
            ReinforcementCoeffContext context,
            string kitFilter)
        {
            bool byGroupingValue = context.GroupingMode == ReinforcementCoeffGroupingMode.ByGroupingValue;
            string parameterName = context.GroupingParameterName?.Trim() ?? string.Empty;

            var entries = new List<ElementGroupEntry>();
            foreach (Element element in CollectRebarElements(doc))
            {
                if (!TryResolveRebarKit(element, kitFilter, out string kit))
                {
                    continue;
                }

                if (!MatchesAdditionalFilters(
                        element,
                        context.AdditionalFilters,
                        ReinforcementCoeffFilterTarget.Rebar))
                {
                    continue;
                }

                string groupingKey = byGroupingValue
                    ? ReadGroupingParameterValue(element, parameterName)
                    : ElevationGroupingHelper.GetElementElevationGroupKey(element);
                if (string.IsNullOrEmpty(groupingKey))
                {
                    continue;
                }

                entries.Add(new ElementGroupEntry(element, kit, groupingKey));
            }

            return entries;
        }

        /// <summary>Раскладывает элементы по ключу (значение группировки, комплект) для O(1)-поиска группы.</summary>
        private static Dictionary<(string GroupingValue, string KitName), List<Element>> BuildMembersMap(
            IReadOnlyList<ElementGroupEntry> entries)
        {
            var membersByGroup = new Dictionary<(string GroupingValue, string KitName), List<Element>>(
                GroupingValueKitPairComparer.Instance);

            foreach (ElementGroupEntry entry in entries)
            {
                var key = (entry.GroupingKey, entry.Kit);
                if (!membersByGroup.TryGetValue(key, out List<Element>? members))
                {
                    members = new List<Element>();
                    membersByGroup.Add(key, members);
                }

                members.Add(entry.Element);
            }

            return membersByGroup;
        }

        /// <summary>Элементы одной группы; для группы без элементов — пустой список.</summary>
        private static IReadOnlyList<Element> GetGroupMembers(
            Dictionary<(string GroupingValue, string KitName), List<Element>> membersByGroup,
            (string GroupingValue, string KitName) groupKey)
        {
            return membersByGroup.TryGetValue(groupKey, out List<Element>? members)
                ? members
                : Array.Empty<Element>();
        }

        /// <summary>
        /// Базовый фильтр арматуры из Dynamo: учёт в спецификации.
        /// </summary>
        private static bool MatchesRebarBaseFilters(Element element)
        {
            if (!ReinforcementCoeffParameterHelper.TryReadBoolValue(
                    element,
                    ReinforcementCoeffConstants.ParamSpecAccount,
                    out bool inSpec) ||
                !inSpec)
            {
                return false;
            }

            // В режиме по параметру Dynamo не требует RNF.Марка конструкции.
            return true;
        }

        /// <summary>
        /// Дополнительные условия фильтрации из UI: все применимые к типу элемента условия
        /// должны выполняться (логика AND). Условия с пустым именем параметра пропускаются.
        /// </summary>
        private static bool MatchesAdditionalFilters(
            Element element,
            IReadOnlyList<ReinforcementCoeffFilterCondition>? filters,
            ReinforcementCoeffFilterTarget elementKind)
        {
            if (filters == null || filters.Count == 0)
            {
                return true;
            }

            foreach (ReinforcementCoeffFilterCondition filter in filters)
            {
                if (filter == null || string.IsNullOrWhiteSpace(filter.ParameterName))
                {
                    continue;
                }

                if (filter.Target != ReinforcementCoeffFilterTarget.Both &&
                    filter.Target != elementKind)
                {
                    continue;
                }

                if (!MatchesFilterCondition(element, filter))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Проверяет одно условие фильтрации по текстовому значению параметра.</summary>
        private static bool MatchesFilterCondition(Element element, ReinforcementCoeffFilterCondition filter)
        {
            string? actual = ReadFilterParameterText(element, filter.ParameterName);
            string expected = filter.Value?.Trim() ?? string.Empty;
            bool hasValue = !string.IsNullOrWhiteSpace(actual);

            switch (filter.Operator)
            {
                case ReinforcementCoeffFilterOperator.HasValue:
                    return hasValue;
                case ReinforcementCoeffFilterOperator.IsEmpty:
                    return !hasValue;
                case ReinforcementCoeffFilterOperator.Equals:
                    return hasValue &&
                           string.Equals(actual!.Trim(), expected, StringComparison.OrdinalIgnoreCase);
                case ReinforcementCoeffFilterOperator.NotEquals:
                    return !hasValue ||
                           !string.Equals(actual!.Trim(), expected, StringComparison.OrdinalIgnoreCase);
                case ReinforcementCoeffFilterOperator.Contains:
                    return hasValue &&
                           actual!.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
                default:
                    return true;
            }
        }

        /// <summary>Читает отображаемое или строковое значение параметра для фильтрации.</summary>
        private static string? ReadFilterParameterText(Element element, string parameterName)
        {
            return ReinforcementCoeffParameterHelper.ReadDisplayTextValue(element, parameterName) ??
                   ReinforcementCoeffParameterHelper.ReadStringValue(element, parameterName);
        }

        /// <summary>True, если параметр существует и содержит непустой текст.</summary>
        private static bool HasNonEmptyParameterValue(Element element, string parameterName) =>
            !string.IsNullOrWhiteSpace(ReadFilterParameterText(element, parameterName));

        /// <summary>
        /// Исключение каркасов (узел Dynamo «Исключение каркасов»):
        /// проверяем «Ключевая пометка» на типе и на экземпляре.
        /// </summary>
        private static bool IsFrameElement(Element element)
        {
            Element? type = element.Document.GetElement(element.GetTypeId());
            string? keyMark = type == null ? null : ReinforcementCoeffParameterHelper.ReadStringValue(
                type, ReinforcementCoeffConstants.ParamKeyMark);
            return string.Equals(keyMark, ReinforcementCoeffConstants.FrameMarkValue, StringComparison.Ordinal);
        }

        /// <summary>Описание расчётной группы: комплект, ключ группировки и текст для отчёта.</summary>
        private sealed class GroupDefinition
        {
            private GroupDefinition(string kitName, string groupingKey, string groupingDisplay)
            {
                KitName = kitName;
                GroupingKey = groupingKey;
                GroupingDisplay = groupingDisplay;
            }

            public string KitName { get; }

            /// <summary>Ключ для сопоставления с элементами (сырое значение, не текст отчёта).</summary>
            public string GroupingKey { get; }

            public string GroupingDisplay { get; }

            public static GroupDefinition ForParameterGrouping(string groupingValue, string kitName) =>
                new GroupDefinition(kitName, groupingValue, groupingValue);

            public static GroupDefinition ForElevationAndKit(string elevationKey, string kitName)
            {
                bool hasElevation = !string.Equals(
                    elevationKey,
                    ReinforcementCoeffConstants.NoElevationGroupKey,
                    StringComparison.OrdinalIgnoreCase);

                return new GroupDefinition(
                    kitName,
                    elevationKey,
                    hasElevation ? elevationKey : string.Empty);
            }
        }

        /// <summary>Элемент с один раз прочитанными комплектом и ключом группировки.</summary>
        private sealed class ElementGroupEntry
        {
            public ElementGroupEntry(Element element, string kit, string groupingKey)
            {
                Element = element;
                Kit = kit;
                GroupingKey = groupingKey;
            }

            public Element Element { get; }
            public string Kit { get; }
            public string GroupingKey { get; }
        }

        private sealed class GroupingValueKitPairComparer : IEqualityComparer<(string GroupingValue, string KitName)>
        {
            public static GroupingValueKitPairComparer Instance { get; } = new GroupingValueKitPairComparer();

            public bool Equals((string GroupingValue, string KitName) x, (string GroupingValue, string KitName) y) =>
                string.Equals(x.GroupingValue, y.GroupingValue, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.KitName, y.KitName, StringComparison.OrdinalIgnoreCase);

            public int GetHashCode((string GroupingValue, string KitName) obj) =>
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.GroupingValue) ^
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.KitName);
        }

        private sealed class ElevationKitPairComparer : IEqualityComparer<(string ElevationKey, string KitName)>
        {
            public static ElevationKitPairComparer Instance { get; } = new ElevationKitPairComparer();

            public bool Equals((string ElevationKey, string KitName) x, (string ElevationKey, string KitName) y) =>
                string.Equals(x.ElevationKey, y.ElevationKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.KitName, y.KitName, StringComparison.OrdinalIgnoreCase);

            public int GetHashCode((string ElevationKey, string KitName) obj) =>
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ElevationKey) ^
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.KitName);
        }

        private sealed class ElementIdComparer : IEqualityComparer<Element>
        {
            public bool Equals(Element? x, Element? y)
            {
                if (x == null && y == null)
                {
                    return true;
                }

                if (x == null || y == null)
                {
                    return false;
                }

                return x.Id == y.Id;
            }

            public int GetHashCode(Element obj) => obj.Id.GetHashCode();
        }
    }
}
