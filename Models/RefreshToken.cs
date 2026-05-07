using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PowerGuardCoreApi.Models
{
    public class RefreshToken
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RefreshTokenId { get; set; }

        [Required]
        public string Token { get; set; } = null!;

        public DateTime? Expires { get; set; }

        [Required]
        public DateTime Created { get; set; } = DateTime.UtcNow;

        public string? CreatedByIp { get; set; }

        public DateTime? Revoked { get; set; }

        public string? RevokedByIp { get; set; }

        public string? ReplacedByToken { get; set; }

        [Required]
        public int AccountId { get; set; }

        [ForeignKey("AccountId")]
        public Account? Account { get; set; }

        [NotMapped]
        public bool IsExpired => Expires.HasValue && DateTime.UtcNow >= Expires.Value;

        [NotMapped]
        public bool IsActive => !Revoked.HasValue && !IsExpired;
    }
}