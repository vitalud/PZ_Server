using DynamicData;
using ReactiveUI;
using Strategies.Strategies;
using System.Collections.ObjectModel;

namespace Server.ViewModels
{
    public partial class StrategiesViewModel : ReactiveObject
    {
        private readonly StrategiesRepository _strategies;

        private readonly ReadOnlyObservableCollection<Strategy> _items;
        public ReadOnlyObservableCollection<Strategy> Items => _items;

        public StrategiesViewModel(StrategiesRepository strategies)
        {
            _strategies = strategies;
            _strategies.StrategiesList.Connect()
               .Bind(out _items)
               .Subscribe();
        }
    }
}
