using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PowerGuardCoreApi._Helpers;

namespace PowerGuardCoreApi.Models
{
    public class Room
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RoomId { get; set; }

        [Required]
        public string RoomName { get; set; } = null!;

        [Required]
        public int RoomNumber { get; set; }

        [Required]
        public int Floor { get; set; }

        public string? Building { get; set; }

        [Required]
        public string DeviceId { get; set; } = null!;

        public bool IsActive { get; set; } = true;

        public DateTime? InactiveSince { get; set; }

        public DateTime? LastActiveAt { get; set; }

        [Required]
        [Column(TypeName = "varchar(8)")]
        public string PowerStatus { get; set; } = "off";

        public string? DeviceKeyEncrypted { get; set; }

        public string? DeviceKeyHash { get; set; }

        public DateTime? DeviceKeyCreatedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ICollection<Account> Accounts { get; set; } = new List<Account>();
    }
}