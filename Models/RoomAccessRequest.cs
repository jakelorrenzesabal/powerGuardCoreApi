using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PowerGuardCoreApi.Models
{
    public class RoomAccessRequest
    {
        [Key]
        public int RequestId { get; set; }

        [Required]
        public int AccountId { get; set; }
        public Account? Account { get; set; }

        [Required]
        public int RoomId { get; set; }
        public Room? Room { get; set; }

        [Required]
        [MaxLength(20)]
        public string RequestType { get; set; } = "TimeLimited"; // "TimeLimited" or "Permanent"

        public DateTime? RequestedExpiryDate { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending"; // "Pending", "Approved", "Rejected"

        [MaxLength(500)]
        public string? AdminComment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
