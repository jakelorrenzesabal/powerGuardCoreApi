using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PowerGuardCoreApi.Models
{
    public class ValidationAttempt
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AttemptId { get; set; }

        [Required]
        public string CardUID { get; set; } = null!;

        [Required]
        public string DeviceId { get; set; } = null!;

        [Required]
        public bool Authorized { get; set; }

        [Required]
        public string Message { get; set; } = null!;

        public int? AccountId { get; set; }
        [ForeignKey("AccountId")]
        public Account? Account { get; set; }

        public int? RoomId { get; set; }
        [ForeignKey("RoomId")]
        public Room? Room { get; set; }

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}