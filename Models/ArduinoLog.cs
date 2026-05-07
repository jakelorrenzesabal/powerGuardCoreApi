using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PowerGuardCoreApi.Models
{
    public class ArduinoLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CardLogId { get; set; }

        [Required]
        public string CardUID { get; set; } = null!;

        [Required]
        public string Event { get; set; } = null!;

        public string? Details { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public int? AccountId { get; set; }
        [ForeignKey("AccountId")]
        public Account? Account { get; set; }

        public int? RoomId { get; set; }
        [ForeignKey("RoomId")]
        public Room? Room { get; set; }
    }
}