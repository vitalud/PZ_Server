using DynamicData;
using ProjectZeroLib;
using ReactiveUI;
using System.Collections.ObjectModel;

namespace Server.ViewModels
{
    public class StrategiesViewModel : ReactiveObject
    {
        private readonly Strategies _strategies;

        private readonly ReadOnlyObservableCollection<Strategy> _items;
        public ReadOnlyObservableCollection<Strategy> Items => _items;

        public StrategiesViewModel(Strategies strategies)
        {
            _strategies = strategies;
            _strategies.StrategiesList.Connect()
               .Bind(out _items)
               .Subscribe();
        }
    }
}
