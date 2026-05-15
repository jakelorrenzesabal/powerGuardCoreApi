using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PowerGuardCoreApi.Models;

namespace PowerGuardCoreApi.Services
{
    public class RoomExpiryWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RoomExpiryWorker> _logger;

        public RoomExpiryWorker(IServiceProvider serviceProvider, ILogger<RoomExpiryWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Room Expiry Worker is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<PowerGuardDbContext>();
                        
                        var expiredAccess = await db.AccountRooms
                            .Include(ar => ar.Account)
                            .Include(ar => ar.Room)
                            .Where(ar => ar.ExpiryDate != null && ar.ExpiryDate < DateTime.UtcNow)
                            .ToListAsync();

                        if (expiredAccess.Any())
                        {
                            _logger.LogInformation($"Removing {expiredAccess.Count} expired room access records.");

                            foreach (var access in expiredAccess)
                            {
                                db.ActivityLogs.Add(new ActivityLog
                                {
                                    AccountId = access.AccountId,
                                    ActionType = "room_expired",
                                    ActionDetails = $"Access to Room '{access.Room?.RoomName}' has expired for user {access.Account?.Email}.",
                                    Timestamp = DateTime.UtcNow
                                });

                                if (access.Room != null && access.Room.PowerStatus == "on")
                                {
                                    access.Room.PowerStatus = "off";
                                    db.ArduinoLogs.Add(new ArduinoLog
                                    {
                                        RoomId = access.RoomId,
                                        Event = "deactivated",
                                        CardUID = "SYSTEM",
                                        Details = $"Room powered off automatically due to expired access for user {access.Account?.Email}.",
                                        Timestamp = DateTime.UtcNow
                                    });
                                }
                            }

                            db.AccountRooms.RemoveRange(expiredAccess);
                            await db.SaveChangesAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while checking for expired room access.");
                }

                // Check every minute
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }

            _logger.LogInformation("Room Expiry Worker is stopping.");
        }
    }
}
