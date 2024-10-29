using CryptoExchange.Net.Clients;
using DynamicData;
using ProjectZeroLib;
using ProjectZeroLib.Enums;
using ProjectZeroLib.Instruments;
using ReactiveUI;
using System.Reactive.Linq;

namespace Server.Service.Abstract
{
    public abstract class BurseModel : ReactiveObject
    {
        protected readonly InstrumentService _instrumentService;

        protected BaseSocketClient _socket;
        protected BaseRestClient _rest;
        protected string[] _keys;

        protected readonly List<int> subToUpdateIds = [];

        private bool _isActive;
        private BurseName _name;
        public bool IsActive
        {
            get => _isActive;
            set => this.RaiseAndSetIfChanged(ref _isActive, value);
        }
        public BurseName Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        private readonly SourceList<Instrument> _instruments = new();
        public SourceList<Instrument> Instruments => _instruments;
        public BurseModel(InstrumentService instrumentService, BurseName name)
        {
            _instrumentService = instrumentService;
            _name = name;

            GetSortedInstruments();
        }

        public async void Connect()
        {
            SetupClientsAsync();
            await GetSubscriptions();
            IsActive = true;
        }
        public async void Disconnect()
        {
            if (_socket != null && _rest != null)
            {
                await _socket.UnsubscribeAllAsync();
                _socket.Dispose();
                _rest.Dispose();
                subToUpdateIds.Clear();
                IsActive = false;
                foreach (var item in Instruments.Items)
                {
                    item.IsActive = false;
                }
            }
        }
        private void GetSortedInstruments()
        {
            var filtered = _instrumentService.Instruments.Items.Where(x => x.Burse.Equals(Name));
            Instruments.AddRange(filtered);

            foreach (var instrument in Instruments.Items)
            {
                instrument.Name.WhenAnyValue(x => x.Expiration)
                    .Skip(1)
                    .Subscribe(async _ => await UpdateSubOnExpire(instrument));
            }
        }
        protected async Task UpdateSubOnExpire(Instrument inst)
        {
            inst.IsActive = false;
            await Subscribe(inst);
        }
        protected abstract void SetupClientsAsync();
        protected abstract Task GetSubscriptions();
        protected abstract Task Subscribe(Instrument instrument);

    }
}
