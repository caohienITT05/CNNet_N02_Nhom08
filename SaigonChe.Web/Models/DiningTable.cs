using System.ComponentModel.DataAnnotations;

namespace SaigonChe.Web.Models
{
    public class DiningTable
    {
        public int Id { get; set; }

        [Required, StringLength(30)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(80)]
        public string Name { get; set; } = string.Empty;

        [Range(1, 100)]
        public int Capacity { get; set; } = 4;

        public bool IsActive { get; set; } = true;

        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
