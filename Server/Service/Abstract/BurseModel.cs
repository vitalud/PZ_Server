using CryptoExchange.Net.Clients;
using DynamicData;
using ProjectZeroLib;
using ProjectZeroLib.Enums;
using ProjectZeroLib.Instruments;
using ReactiveUI;
using System.Collections.ObjectModel;

namespace Server.Service.Abstract
{
    public abstract class BurseModel : ReactiveObject
    {
        protected readonly InstrumentService _instrumentService;

        protected BaseSocketClient socket;
        protected BaseRestClient rest;
        protected string[] keys;

        protected List<int> subToUpdateIds;

        public SourceList<Instrument> Instruments { get; private set; }

        public BurseModel(InstrumentService instrumentService)
        {
            _instrumentService = instrumentService;
            Instruments = new SourceList<Instrument>();

        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set => this.RaiseAndSetIfChanged(ref _isActive, value);
        }

        private BurseName _name;
        public BurseName Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        private ObservableCollection<Signal> _generatedSignals;
        public ObservableCollection<Signal> GeneratedSignals
        {
            get => _generatedSignals;
            set => this.RaiseAndSetIfChanged(ref _generatedSignals, value);
        }

        public async void Connect()
        {
            SetupClientsAsync();
            if (!Name.Equals(BurseName.Quik))
            {
                subToUpdateIds = [];
                await GetSubscriptions();
            }
            IsActive = true;
        }
        public async void Disconnect()
        {
            if (socket != null)
            {
                await socket.UnsubscribeAllAsync();
                socket.Dispose();
                rest.Dispose();
                subToUpdateIds.Clear();
                Instruments.Clear();
            }
            IsActive = false;
        }

        protected async void UpdateSubOnExpire(Instrument inst)
        {
            IsActive = false;
            inst.IsActive = false;
            await Subscribe(inst);
            IsActive = true;
        }

        protected abstract void SetupClientsAsync();
        protected abstract Task GetSubscriptions();
        protected abstract Task Subscribe(Instrument instrument);

    }
}
