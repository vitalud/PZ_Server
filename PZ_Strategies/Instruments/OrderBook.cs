using ReactiveUI;

namespace Strategies.Instruments
{
    /// <summary>
    /// Данные стакана.
    /// </summary>
    public partial class OrderBook : ReactiveObject
    {
        private decimal _allAsks;
        private decimal _bestAsks;
        private decimal _numAsks;
        private decimal _allBids;
        private decimal _bestBids;
        private decimal _numBids;

        public decimal AllAsks
        {
            get => _allAsks;
            set => this.RaiseAndSetIfChanged(ref _allAsks, value);
        }
        public decimal BestAsks
        {
            get => _bestAsks;
            set => this.RaiseAndSetIfChanged(ref _bestAsks, value);
        }
        public decimal NumAsks
        {
            get => _numAsks;
            set => this.RaiseAndSetIfChanged(ref _numAsks, value);
        }
        public decimal AllBids
        {
            get => _allBids;
            set => this.RaiseAndSetIfChanged(ref _allBids, value);
        }
        public decimal BestBids
        {
            get => _bestBids;
            set => this.RaiseAndSetIfChanged(ref _bestBids, value);
        }
        public decimal NumBids
        {
            get => _numBids;
            set => this.RaiseAndSetIfChanged(ref _numBids, value);
        }
    }
}
