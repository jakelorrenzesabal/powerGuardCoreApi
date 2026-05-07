namespace PowerGuardCoreApi._Helpers
{
    public class RoomInactiveException : Exception
    {
        public RoomInactiveException(string message) : base(message) { }
    }

    public class DeviceOfflineException : Exception
    {
        public DeviceOfflineException(string message) : base(message) { }
    }
}
