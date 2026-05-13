using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PowerGuardCoreApi.Models
{
    public class AccountRoom
    {
        public int AccountId { get; set; }
        public Account Account { get; set; } = null!;

        public int RoomId { get; set; }
        public Room Room { get; set; } = null!;

        public DateTime? ExpiryDate { get; set; }
    }
}
