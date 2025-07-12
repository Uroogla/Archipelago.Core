using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Archipelago.Core.Util.GPS
{
    public class GPSHandler
    {
        private Timer _pollingTimer;
        private readonly object _lockObject = new object();
        public int MapId { get; private set; }
        public string MapName { get; private set; }
        public string Region { get; private set; }
        public float X { get; private set; }
        public float Y { get; private set; }
        public float Z { get; private set; }
        public event EventHandler<PositionChangedEventArgs> PositionChanged;
        public event EventHandler<MapChangedEventArgs> MapChanged;
        private Func<PositionData>  _updatePositionCallback;
        private TimeSpan _pollingInterval;
        private bool _disposed;

        public GPSHandler(Func<PositionData> updateCallback, int pollingIntervalMs = 1000)
        {
            _updatePositionCallback = updateCallback ?? throw new ArgumentNullException(nameof(updateCallback));
            _pollingInterval = TimeSpan.FromMilliseconds(pollingIntervalMs);
        }
        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GPSHandler));

            _pollingTimer?.Dispose();
            _pollingTimer = new Timer((o)=> UpdatePosition(), null, TimeSpan.Zero, _pollingInterval);
        }
        public void Stop()
        {
            _pollingTimer?.Dispose();
            _pollingTimer = null;
        }
        internal void UpdatePosition()
        {
            var data = _updatePositionCallback?.Invoke();
            if (!data.HasValue) return;
            var positionData = data.Value;
            var oldMapId = MapId;
            var oldMapName = MapName;
            var oldX = X;
            var oldY = Y;
            var oldZ = Z;
            MapId = positionData.MapId;
            MapName = positionData.MapName;
            X = positionData.X;
            Y = positionData.Y;
            Z = positionData.Z;
            Region = positionData.Region;
            if(oldMapId != MapId || oldMapName != MapName)
            {
                MapChanged?.Invoke(this, new MapChangedEventArgs(oldMapId, oldMapName, MapId, MapName));
            }
            if (oldX != X || oldY != Y || oldZ != Z)
            {
                PositionChanged?.Invoke(this, new PositionChangedEventArgs(oldX, oldY, oldZ, X, Y, Z));
            }
        }
        public void SetInterval(int pollingIntervalMs)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GPSHandler));

            _pollingInterval = TimeSpan.FromMilliseconds(pollingIntervalMs);

            // Restart timer with new interval if currently running
            if (_pollingTimer != null)
            {
                _pollingTimer.Change(TimeSpan.Zero, _pollingInterval);
            }
        }
        public PositionData GetCurrentPosition()
        {
            lock (_lockObject)
            {
                return new PositionData
                {
                    MapId = MapId,
                    MapName = MapName,
                    Region = Region,
                    X = X,
                    Y = Y,
                    Z = Z
                };
            }
        }
        public void Dispose()
        {
            if (!_disposed)
            {
                _pollingTimer?.Dispose();
                _disposed = true;
            }
        }
    }
}
