using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BrickVault
{
    public enum BuildStatus
    {
        ScanningFiles,
        PackingFiles,
        WritingHeader,
        Finalising,
        Done,
        Cancelled
    }

    public class BuildProgress : INotifyPropertyChanged
    {
        private BuildStatus _status;
        private int _current;
        private int _total;

        public BuildStatus Status
        {
            get => _status;
            set => SetField(ref _status, value);
        }

        public int Current
        {
            get => _current;
            set => SetField(ref _current, value);
        }

        public int Total
        {
            get => _total;
            set => SetField(ref _total, value);
        }

        public CancellationTokenSource CancellationTokenSource { get; private set; } = new();
        public CancellationToken CancellationToken => CancellationTokenSource.Token;

        public void Cancel() => CancellationTokenSource.Cancel();

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }

}
