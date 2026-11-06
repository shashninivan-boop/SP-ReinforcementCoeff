using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using SP_Addin.ReinforcementCoeff.Infrastructure;

using SP_Addin.ReinforcementCoeff.Models;

namespace SP_Addin.ReinforcementCoeff
{
    /// <summary>Группа арматуры на одном owner-view в панели состава.</summary>
    public sealed class ReinforcementCoeffCompositionViewGroup
    {
        private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

        public ReinforcementCoeffCompositionViewGroup(
            string viewName,
            long ownerViewId,
            IReadOnlyList<ReinforcementCoeffElementContribution> items)
        {
            ViewName = viewName;
            OwnerViewId = ownerViewId;
            Items = new ObservableCollection<ReinforcementCoeffElementContribution>(items);
            TotalContribution = items.Sum(item => item.Contribution);
        }

        public string ViewName { get; }
        public long OwnerViewId { get; }
        public double TotalContribution { get; }
        public ObservableCollection<ReinforcementCoeffElementContribution> Items { get; }

        public bool HasOwnerView => OwnerViewId > 0;

        public string HeaderDisplay =>
            string.Format(
                RuCulture,
                "{0} · {1} эл. · {2:0.###}",
                ViewName,
                Items.Count,
                TotalContribution);

        public IReadOnlyList<long> ElementIds =>
            Items.Select(item => item.ElementId).Where(id => id > 0).ToList();
    }

    /// <summary>ViewModel окна поэлементного состава группы.</summary>
    public partial class ReinforcementCoeffCompositionViewModel : ObservableObject
    {
        private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

        private readonly Action<IReadOnlyList<long>, long?> _requestSelect;
        private readonly Action _requestClose;
        private bool _isConcreteTabSelected = true;

        public ReinforcementCoeffCompositionViewModel(
            ReinforcementCoeffGroupResult group,
            ReinforcementCoeffContributionKind initialKind,
            Action<IReadOnlyList<long>, long?> requestSelect,
            Action requestClose)
        {
            Group = group ?? throw new ArgumentNullException(nameof(group));
            _requestSelect = requestSelect ?? throw new ArgumentNullException(nameof(requestSelect));
            _requestClose = requestClose ?? throw new ArgumentNullException(nameof(requestClose));

            ConcreteItems = new ObservableCollection<ReinforcementCoeffElementContribution>(
                group.ConcreteContributions ?? new List<ReinforcementCoeffElementContribution>());
            RebarViewGroups = new ObservableCollection<ReinforcementCoeffCompositionViewGroup>(
                BuildRebarViewGroups(group.RebarContributions));

            _isConcreteTabSelected = initialKind == ReinforcementCoeffContributionKind.Concrete;

            NavigateContributionCommand = new RelayCommand<ReinforcementCoeffElementContribution>(NavigateContribution);
            NavigateViewGroupCommand = new RelayCommand<ReinforcementCoeffCompositionViewGroup>(NavigateViewGroup);
            ShowConcreteTabCommand = new RelayCommand(() => IsConcreteTabSelected = true);
            ShowRebarTabCommand = new RelayCommand(() => IsConcreteTabSelected = false);
            SelectAllCommand = new RelayCommand(SelectAllCurrentTab, CanSelectAllCurrentTab);
            CloseCommand = new RelayCommand(() => _requestClose());
        }

        public ReinforcementCoeffGroupResult Group { get; }

        public ObservableCollection<ReinforcementCoeffElementContribution> ConcreteItems { get; }

        public ObservableCollection<ReinforcementCoeffCompositionViewGroup> RebarViewGroups { get; }

        public string TitleText
        {
            get
            {
                string kit = string.IsNullOrWhiteSpace(Group.KitName) ? "(все комплекты)" : Group.KitName;
                string grouping = string.IsNullOrWhiteSpace(Group.GroupingDisplay)
                    ? "(без группирования)"
                    : Group.GroupingDisplay;
                return kit + " · " + grouping;
            }
        }

        public string ConcreteSummaryText =>
            string.Format(
                RuCulture,
                "Бетон: {0} эл., сумма {1}",
                ConcreteItems.Count,
                Group.ConcreteVolumeDisplay);

        public string RebarSummaryText =>
            string.Format(
                RuCulture,
                "Арматура: {0} эл., {1} вид., сумма {2}",
                RebarViewGroups.Sum(group => group.Items.Count),
                RebarViewGroups.Count,
                Group.RebarMassDisplay);

        public bool IsConcreteTabSelected
        {
            get => _isConcreteTabSelected;
            set
            {
                if (SetProperty(ref _isConcreteTabSelected, value))
                {
                    OnPropertyChanged(nameof(IsRebarTabSelected));
                    SelectAllCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool IsRebarTabSelected => !_isConcreteTabSelected;

        public RelayCommand<ReinforcementCoeffElementContribution> NavigateContributionCommand { get; }
        public RelayCommand<ReinforcementCoeffCompositionViewGroup> NavigateViewGroupCommand { get; }
        public RelayCommand ShowConcreteTabCommand { get; }
        public RelayCommand ShowRebarTabCommand { get; }
        public RelayCommand SelectAllCommand { get; }
        public RelayCommand CloseCommand { get; }

        /// <summary>Переход к одному элементу (для узлов — на owner-view).</summary>
        private void NavigateContribution(ReinforcementCoeffElementContribution? item)
        {
            if (item == null || item.ElementId <= 0)
            {
                return;
            }

            long? targetViewId = item.Kind == ReinforcementCoeffContributionKind.Rebar && item.HasOwnerView
                ? item.OwnerViewId
                : null;
            _requestSelect(new[] { item.ElementId }, targetViewId);
        }

        /// <summary>Открывает вид и выделяет все узлы группы на нём.</summary>
        private void NavigateViewGroup(ReinforcementCoeffCompositionViewGroup? viewGroup)
        {
            if (viewGroup == null || viewGroup.ElementIds.Count == 0)
            {
                return;
            }

            long? targetViewId = viewGroup.HasOwnerView ? viewGroup.OwnerViewId : null;
            _requestSelect(viewGroup.ElementIds, targetViewId);
        }

        private bool CanSelectAllCurrentTab()
        {
            return IsConcreteTabSelected
                ? ConcreteItems.Count > 0
                : RebarViewGroups.Any(group => group.Items.Count > 0);
        }

        /// <summary>Выделяет все элементы активной вкладки (без принудительной смены вида).</summary>
        private void SelectAllCurrentTab()
        {
            if (IsConcreteTabSelected)
            {
                IReadOnlyList<long> ids = ConcreteItems
                    .Select(item => item.ElementId)
                    .Where(id => id > 0)
                    .ToList();
                if (ids.Count == 0)
                {
                    return;
                }

                _requestSelect(ids, null);
                return;
            }

            IReadOnlyList<long> rebarIds = RebarViewGroups
                .SelectMany(group => group.ElementIds)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            if (rebarIds.Count == 0)
            {
                return;
            }

            // Для смешанных видов не задаём TargetViewId — ShowElements как раньше.
            _requestSelect(rebarIds, null);
        }

        private static IReadOnlyList<ReinforcementCoeffCompositionViewGroup> BuildRebarViewGroups(
            IReadOnlyList<ReinforcementCoeffElementContribution>? contributions)
        {
            if (contributions == null || contributions.Count == 0)
            {
                return Array.Empty<ReinforcementCoeffCompositionViewGroup>();
            }

            return contributions
                .GroupBy(
                    item => (item.OwnerViewId, ViewName: string.IsNullOrWhiteSpace(item.OwnerViewName)
                        ? "(без вида)"
                        : item.OwnerViewName),
                    (key, items) => new ReinforcementCoeffCompositionViewGroup(
                        key.ViewName,
                        key.OwnerViewId,
                        items
                            .OrderByDescending(item => item.Contribution)
                            .ToList()))
                .OrderByDescending(group => group.TotalContribution)
                .ToList();
        }
    }
}
