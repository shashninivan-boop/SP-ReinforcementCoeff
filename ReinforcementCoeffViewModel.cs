using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Autodesk.Revit.UI;
using SP_Addin.ReinforcementCoeff.Infrastructure;

using SP_Addin.ReinforcementCoeff.Models;
using SP_Addin.ReinforcementCoeff.Services;

namespace SP_Addin.ReinforcementCoeff
{
    /// <summary>Пункт выпадающего списка оператора фильтра.</summary>
    public sealed class ReinforcementCoeffFilterOperatorOption
    {
        public ReinforcementCoeffFilterOperatorOption(
            ReinforcementCoeffFilterOperator value,
            string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public ReinforcementCoeffFilterOperator Value { get; }
        public string DisplayName { get; }
    }

    /// <summary>Пункт выпадающего списка области применения фильтра.</summary>
    public sealed class ReinforcementCoeffFilterTargetOption
    {
        public ReinforcementCoeffFilterTargetOption(
            ReinforcementCoeffFilterTarget value,
            string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public ReinforcementCoeffFilterTarget Value { get; }
        public string DisplayName { get; }
    }

    /// <summary>Строка дополнительного условия фильтрации в UI.</summary>
    public partial class ReinforcementCoeffFilterConditionItem : ObservableObject
    {
        private string _parameterName = string.Empty;
        private ReinforcementCoeffFilterOperator _operator = ReinforcementCoeffFilterOperator.Equals;
        private string _value = string.Empty;
        private ReinforcementCoeffFilterTarget _target = ReinforcementCoeffFilterTarget.Rebar;

        /// <summary>Имя параметра Revit.</summary>
        public string ParameterName
        {
            get => _parameterName;
            set => SetProperty(ref _parameterName, value);
        }

        /// <summary>Оператор сравнения.</summary>
        public ReinforcementCoeffFilterOperator Operator
        {
            get => _operator;
            set
            {
                if (SetProperty(ref _operator, value))
                {
                    OnPropertyChanged(nameof(IsValueEnabled));
                }
            }
        }

        /// <summary>Ожидаемое значение (для Equals / NotEquals / Contains).</summary>
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        /// <summary>К каким элементам применяется условие.</summary>
        public ReinforcementCoeffFilterTarget Target
        {
            get => _target;
            set => SetProperty(ref _target, value);
        }

        /// <summary>Поле значения нужно только для операторов со сравнением текста.</summary>
        public bool IsValueEnabled =>
            Operator == ReinforcementCoeffFilterOperator.Equals ||
            Operator == ReinforcementCoeffFilterOperator.NotEquals ||
            Operator == ReinforcementCoeffFilterOperator.Contains;

        /// <summary>Снимок условия для передачи в сервис расчёта.</summary>
        public ReinforcementCoeffFilterCondition ToModel()
        {
            return new ReinforcementCoeffFilterCondition
            {
                ParameterName = ParameterName?.Trim() ?? string.Empty,
                Operator = Operator,
                Value = Value?.Trim() ?? string.Empty,
                Target = Target,
            };
        }
    }

    /// <summary>ViewModel окна расчёта коэффициента армирования.</summary>
    public partial class ReinforcementCoeffViewModel : ObservableObject, IDisposable
    {
        private readonly UIApplication _uiApp;
        private readonly ReinforcementCoeffRunHandler _runHandler;
        private readonly ReinforcementCoeffWriteHandler _writeHandler;
        private readonly ReinforcementCoeffSelectElementsHandler _selectElementsHandler;
        private readonly ExternalEvent _runEvent;
        private readonly ExternalEvent _writeEvent;
        private readonly ExternalEvent _selectElementsEvent;
        private readonly Dispatcher _uiDispatcher;
        private ReinforcementCoeffCompositionView? _compositionView;
        private CalculationSnapshot? _snapshot;
        private bool _disposed;
        private int _settingsVersion;
        private int _runSettingsVersion;

        protected override void OnPropertyChanged(string? name = null)
        {
            base.OnPropertyChanged(name);
            if (name == nameof(KitName) || name == nameof(GroupingParameterName) || name == nameof(GroupingMode))
                OnSettingsChanged();
        }

        private void OnSettingsChanged()
        {
            _settingsVersion++;
            if (_snapshot == null) return;
            ReleaseSnapshot();
            SetPendingWrites(Array.Empty<ReinforcementCoeffParameterWrite>());
            _compositionView?.Close();
            SummaryText = "Настройки изменены. Выполните расчёт заново.";
        }

        private void OnFilterChanged(object? sender, PropertyChangedEventArgs e) => OnSettingsChanged();
        private void OnFiltersChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (ReinforcementCoeffFilterConditionItem item in e.OldItems) item.PropertyChanged -= OnFilterChanged;
            if (e.NewItems != null)
                foreach (ReinforcementCoeffFilterConditionItem item in e.NewItems) item.PropertyChanged += OnFilterChanged;
            OnSettingsChanged();
        }

        private void ReleaseSnapshot()
        {
            if (_snapshot != null)
            {
                _snapshot.Invalidated -= OnSnapshotInvalidated;
                _snapshot.Dispose();
                _snapshot = null;
            }
        }

        private void OnSnapshotInvalidated()
        {
            _uiDispatcher.BeginInvoke(new Action(() =>
            {
                if (_disposed || _snapshot?.IsStale != true) return;
                SetPendingWrites(Array.Empty<ReinforcementCoeffParameterWrite>());
                if (!IsWriting)
                    SummaryText = "Модель изменилась. Отчёт устарел: выполните расчёт заново.";
                _compositionView?.Close();
            }));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ReleaseSnapshot();
            AdditionalFilters.CollectionChanged -= OnFiltersChanged;
            foreach (var item in AdditionalFilters) item.PropertyChanged -= OnFilterChanged;
            _compositionView?.Close();
            _runEvent.Dispose();
            _writeEvent.Dispose();
            _selectElementsEvent.Dispose();
        }

        private ReinforcementCoeffGroupingMode _groupingMode = ReinforcementCoeffGroupingMode.ByGroupingValue;
        private string _kitName = string.Empty;
        private string _groupingParameterName = ReinforcementCoeffConstants.ParamGrouping;
        private string _summaryText = string.Empty;
        private bool _isRunning;
        private bool _isWriting;
        private List<ReinforcementCoeffParameterWrite> _pendingWrites = new List<ReinforcementCoeffParameterWrite>();

        /// <summary>Операторы для ComboBox дополнительных условий.</summary>
        public IReadOnlyList<ReinforcementCoeffFilterOperatorOption> FilterOperatorOptions { get; } =
            new[]
            {
                new ReinforcementCoeffFilterOperatorOption(
                    ReinforcementCoeffFilterOperator.HasValue,
                    "Заполнен"),
                new ReinforcementCoeffFilterOperatorOption(
                    ReinforcementCoeffFilterOperator.IsEmpty,
                    "Пуст"),
                new ReinforcementCoeffFilterOperatorOption(
                    ReinforcementCoeffFilterOperator.Equals,
                    "Равен"),
                new ReinforcementCoeffFilterOperatorOption(
                    ReinforcementCoeffFilterOperator.NotEquals,
                    "Не равен"),
                new ReinforcementCoeffFilterOperatorOption(
                    ReinforcementCoeffFilterOperator.Contains,
                    "Содержит"),
            };

        /// <summary>Области применения для ComboBox дополнительных условий.</summary>
        public IReadOnlyList<ReinforcementCoeffFilterTargetOption> FilterTargetOptions { get; } =
            new[]
            {
                new ReinforcementCoeffFilterTargetOption(
                    ReinforcementCoeffFilterTarget.Rebar,
                    "Арматура"),
                new ReinforcementCoeffFilterTargetOption(
                    ReinforcementCoeffFilterTarget.Concrete,
                    "Бетон"),
                new ReinforcementCoeffFilterTargetOption(
                    ReinforcementCoeffFilterTarget.Both,
                    "Оба"),
            };

        public ObservableCollection<ReinforcementCoeffGroupResult> Groups { get; } =
            new ObservableCollection<ReinforcementCoeffGroupResult>();

        /// <summary>Дополнительные условия фильтрации, задаваемые пользователем.</summary>
        public ObservableCollection<ReinforcementCoeffFilterConditionItem> AdditionalFilters { get; } =
            new ObservableCollection<ReinforcementCoeffFilterConditionItem>();
        public ReinforcementCoeffGroupingMode GroupingMode
        {
            get => _groupingMode;
            set
            {
                if (SetProperty(ref _groupingMode, value))
                {
                    OnPropertyChanged(nameof(IsGroupingValueMode));
                    OnPropertyChanged(nameof(IsElevationMode));
                    OnPropertyChanged(nameof(IsGroupingValueModeSelected));
                    OnPropertyChanged(nameof(IsElevationModeSelected));
                }
            }
        }

        public bool IsGroupingValueMode =>
            GroupingMode == ReinforcementCoeffGroupingMode.ByGroupingValue;

        public bool IsElevationMode =>
            GroupingMode == ReinforcementCoeffGroupingMode.ByElevation;

        public bool IsGroupingValueModeSelected
        {
            get => IsGroupingValueMode;
            set
            {
                if (value)
                {
                    GroupingMode = ReinforcementCoeffGroupingMode.ByGroupingValue;
                }
            }
        }

        public bool IsElevationModeSelected
        {
            get => IsElevationMode;
            set
            {
                if (value)
                {
                    GroupingMode = ReinforcementCoeffGroupingMode.ByElevation;
                }
            }
        }

        public string KitName
        {
            get => _kitName;
            set => SetProperty(ref _kitName, value);
        }

        public string GroupingParameterName
        {
            get => _groupingParameterName;
            set => SetProperty(ref _groupingParameterName, value);
        }

        public string SummaryText
        {
            get => _summaryText;
            set => SetProperty(ref _summaryText, value);
        }

        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (SetProperty(ref _isRunning, value))
                {
                    RunCommand.NotifyCanExecuteChanged();
                    WriteValuesCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool IsWriting
        {
            get => _isWriting;
            set
            {
                if (SetProperty(ref _isWriting, value))
                {
                    RunCommand.NotifyCanExecuteChanged();
                    WriteValuesCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool CanWriteValues => _pendingWrites.Count > 0;

        public RelayCommand RunCommand { get; }
        public RelayCommand WriteValuesCommand { get; }
        public RelayCommand AddFilterConditionCommand { get; }
        public RelayCommand<ReinforcementCoeffFilterConditionItem> RemoveFilterConditionCommand { get; }
        public RelayCommand<ReinforcementCoeffGroupResult> SelectConcreteElementsCommand { get; }
        public RelayCommand<ReinforcementCoeffGroupResult> SelectRebarElementsCommand { get; }

        public ReinforcementCoeffViewModel(UIApplication uiApp)
        {
            _uiApp = uiApp;
            _uiDispatcher = Dispatcher.CurrentDispatcher;
            AdditionalFilters.CollectionChanged += OnFiltersChanged;
            _runHandler = new ReinforcementCoeffRunHandler();
            _writeHandler = new ReinforcementCoeffWriteHandler();
            _selectElementsHandler = new ReinforcementCoeffSelectElementsHandler();
            _runEvent = ExternalEvent.Create(_runHandler);
            _writeEvent = ExternalEvent.Create(_writeHandler);
            _selectElementsEvent = ExternalEvent.Create(_selectElementsHandler);
            RunCommand = new RelayCommand(RunCalculation, () => !IsRunning && !IsWriting);
            WriteValuesCommand = new RelayCommand(WriteValues, () => !IsRunning && !IsWriting && CanWriteValues);
            AddFilterConditionCommand = new RelayCommand(AddFilterCondition);
            RemoveFilterConditionCommand = new RelayCommand<ReinforcementCoeffFilterConditionItem>(RemoveFilterCondition);
            SelectConcreteElementsCommand = new RelayCommand<ReinforcementCoeffGroupResult>(SelectConcreteElements);
            SelectRebarElementsCommand = new RelayCommand<ReinforcementCoeffGroupResult>(SelectRebarElements);
        }

        /// <summary>Добавляет пустую строку дополнительного условия фильтрации.</summary>
        private void AddFilterCondition()
        {
            AdditionalFilters.Add(new ReinforcementCoeffFilterConditionItem());
        }

        /// <summary>Удаляет строку дополнительного условия фильтрации.</summary>
        private void RemoveFilterCondition(ReinforcementCoeffFilterConditionItem? item)
        {
            if (item == null)
            {
                return;
            }

            AdditionalFilters.Remove(item);
        }

        private void SelectConcreteElements(ReinforcementCoeffGroupResult? group) =>
            OpenComposition(group, ReinforcementCoeffContributionKind.Concrete);

        private void SelectRebarElements(ReinforcementCoeffGroupResult? group) =>
            OpenComposition(group, ReinforcementCoeffContributionKind.Rebar);

        /// <summary>Открывает панель поэлементного состава группы.</summary>
        private void OpenComposition(
            ReinforcementCoeffGroupResult? group,
            ReinforcementCoeffContributionKind initialKind)
        {
            if (group == null)
            {
                return;
            }

            bool hasItems = initialKind == ReinforcementCoeffContributionKind.Concrete
                ? group.HasConcreteContributions || group.HasConcreteElementIds
                : group.HasRebarContributions || group.HasRebarElementIds;
            if (!hasItems)
            {
                MessageBox.Show(
                    "В этой группе нет элементов для отображения состава.",
                    "Средний расход арматуры",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                if (_compositionView != null)
                {
                    _compositionView.Close();
                    _compositionView = null;
                }

                var compositionViewModel = new ReinforcementCoeffCompositionViewModel(
                    group,
                    initialKind,
                    RequestSelectElements,
                    () => _compositionView?.Close());
                var compositionView = new ReinforcementCoeffCompositionView(compositionViewModel);

                Window? owner = FindOwnerWindow();
                if (owner != null)
                {
                    compositionView.Owner = owner;
                }
                else
                {
                    compositionView.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                _compositionView = compositionView;
                compositionView.Closed += (_, __) =>
                {
                    if (ReferenceEquals(_compositionView, compositionView))
                    {
                        _compositionView = null;
                    }
                };
                compositionView.Show();
                compositionView.Activate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось открыть состав группы:\n" + ex.Message,
                    "Средний расход арматуры",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>Ищет главное окно отчёта как Owner для панели состава.</summary>
        private Window? FindOwnerWindow()
        {
            try
            {
                if (Application.Current?.Windows == null)
                {
                    return null;
                }

                return Application.Current.Windows
                    .OfType<ReinforcementCoeffView>()
                    .FirstOrDefault(window => ReferenceEquals(window.DataContext, this));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Выделение элементов в Revit; targetViewId активирует owner-view узлов.</summary>
        private void RequestSelectElements(IReadOnlyList<long> elementIds, long? targetViewId)
        {
            if (elementIds == null || elementIds.Count == 0 || _uiApp?.ActiveUIDocument == null)
            {
                return;
            }

            _selectElementsHandler.Snapshot = _snapshot;
            _selectElementsHandler.UIApp = _uiApp;
            _selectElementsHandler.ElementIdValues = elementIds.ToList();
            _selectElementsHandler.TargetViewId = targetViewId;
            _selectElementsEvent.Raise();
        }

        private void RunCalculation()
        {
            if (IsGroupingValueMode && string.IsNullOrWhiteSpace(GroupingParameterName))
            {
                MessageBox.Show(
                    "Укажите параметр для группирования.",
                    "Средний расход арматуры",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            ReleaseSnapshot();
            _runSettingsVersion = _settingsVersion;
            _compositionView?.Close();
            IsRunning = true;
            SummaryText = "Выполняется расчёт...";
            Groups.Clear();
            SetPendingWrites(Array.Empty<ReinforcementCoeffParameterWrite>());

            _runHandler.UIApp = _uiApp;
            _runHandler.Context = new ReinforcementCoeffContext
            {
                KitName = KitName.Trim(),
                GroupingParameterName = GroupingParameterName?.Trim() ?? string.Empty,
                GroupingMode = GroupingMode,
                AdditionalFilters = BuildAdditionalFiltersSnapshot(),
            };
            _runHandler.OnCompleted = OnRunCompleted;

            // Расчёт без записи в параметры — только в обработчике ExternalEvent.
            try
            {
                if (_runEvent.Raise() != ExternalEventRequest.Accepted)
                    throw new InvalidOperationException("Revit не принял запрос расчёта. Повторите действие.");
            }
            catch (Exception ex) { IsRunning = false; SummaryText = ex.Message; }
        }

        private void WriteValues()
        {
            if (_pendingWrites.Count == 0)
            {
                MessageBox.Show(
                    "Нет данных для записи. Сначала выполните расчёт.",
                    "Средний расход арматуры",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            IsWriting = true;
            SummaryText = "Выполняется запись значений в параметры...";

            _writeHandler.UIApp = _uiApp;
            _writeHandler.PendingWrites = _pendingWrites.ToList();
            _writeHandler.Snapshot = _snapshot;
            _writeHandler.OnCompleted = OnWriteCompleted;
            try
            {
                if (_writeEvent.Raise() != ExternalEventRequest.Accepted)
                    throw new InvalidOperationException("Revit не принял запрос записи. Повторите действие.");
            }
            catch (Exception ex) { IsWriting = false; SummaryText = ex.Message; }
        }

        private void SetPendingWrites(IReadOnlyList<ReinforcementCoeffParameterWrite> writes)
        {
            _pendingWrites = writes?.ToList() ?? new List<ReinforcementCoeffParameterWrite>();
            OnPropertyChanged(nameof(CanWriteValues));
            WriteValuesCommand.NotifyCanExecuteChanged();
        }

        /// <summary>Возврат из Revit API-потока в UI-поток WPF.</summary>
        private void OnRunCompleted(ReinforcementCoeffRunResult? result, string? error)
        {
            _uiDispatcher.BeginInvoke(new Action(() =>
            {
                if (_disposed) { result?.Snapshot?.Dispose(); return; }
                IsRunning = false;

                if (!string.IsNullOrWhiteSpace(error))
                {
                    SummaryText = error!;
                    MessageBox.Show(error, "Средний расход арматуры", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (_runSettingsVersion != _settingsVersion)
                {
                    result?.Snapshot?.Dispose();
                    SummaryText = "Настройки изменились во время расчёта. Выполните расчёт заново.";
                    return;
                }
                ReleaseSnapshot();
                _snapshot = result?.Snapshot;
                if (_snapshot != null) _snapshot.Invalidated += OnSnapshotInvalidated;
                if (_snapshot?.IsStale == true)
                {
                    OnSnapshotInvalidated();
                    return;
                }
                Groups.Clear();
                if (result?.Groups != null)
                {
                    foreach (ReinforcementCoeffGroupResult group in result.Groups)
                    {
                        Groups.Add(group);
                    }
                }

                SummaryText = BuildSummaryText(result);
                SetPendingWrites(result?.PendingWrites ?? Array.Empty<ReinforcementCoeffParameterWrite>());
            }));
        }

        private void OnWriteCompleted(ReinforcementCoeffWriteResult? result, string? error)
        {
            _uiDispatcher.BeginInvoke(new Action(() =>
            {
                if (_disposed) return;
                IsWriting = false;

                if (!string.IsNullOrWhiteSpace(error))
                {
                    SummaryText = error!;
                    MessageBox.Show(error, "Средний расход арматуры", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (result == null)
                {
                    return;
                }

                SetPendingWrites(Array.Empty<ReinforcementCoeffParameterWrite>());
                ReleaseSnapshot();
                string calculationSummary = BuildSummaryTextFromGroups();
                SummaryText = string.IsNullOrWhiteSpace(calculationSummary)
                    ? result.SummaryText
                    : calculationSummary + " " + result.SummaryText;
            }));
        }

        private string BuildSummaryText(ReinforcementCoeffRunResult? result)
        {
            if (result == null)
            {
                return string.Empty;
            }

            if (result.Groups == null || result.Groups.Count == 0)
            {
                return result.SummaryText;
            }

            return result.SummaryText + " " + BuildGroupsStatsText(result.Groups);
        }

        private string BuildSummaryTextFromGroups()
        {
            if (Groups.Count == 0)
            {
                return string.Empty;
            }

            return BuildGroupsStatsText(Groups);
        }

        private static string BuildGroupsStatsText(IEnumerable<ReinforcementCoeffGroupResult> groups)
        {
            var allGroups = groups.ToList();
            var successful = allGroups.Where(g => g.IsSuccess).ToList();
            int successCount = successful.Count;
            double totalVolume = successful.Sum(g => g.ConcreteVolume);
            double totalMass = successful.Sum(g => g.RebarMass);
            double average = totalVolume > 0 ? totalMass / totalVolume : 0;

            return string.Format(
                CultureInfo.GetCultureInfo("ru-RU"),
                "Успешно: {0}; с ошибками: {3}. По успешным группам: {1:0.###} м³; {2:0.###} кг; средний расход: {4}.",
                successCount, totalVolume, totalMass, allGroups.Count - successCount,
                totalVolume > 0 ? average.ToString("0.###", CultureInfo.GetCultureInfo("ru-RU")) + " кг/м³" : "—");
        }

        /// <summary>
        /// Снимок доп. условий для потока Revit API: только строки с заполненным именем параметра.
        /// </summary>
        private IReadOnlyList<ReinforcementCoeffFilterCondition> BuildAdditionalFiltersSnapshot()
        {
            return AdditionalFilters
                .Select(item => item.ToModel())
                .Where(condition => !string.IsNullOrWhiteSpace(condition.ParameterName))
                .ToList();
        }
    }
}
