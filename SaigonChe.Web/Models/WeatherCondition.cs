using System.ComponentModel.DataAnnotations;

namespace SaigonChe.Web.Models
{
    public enum WeatherCondition
    {
        [Display(Name = "Nắng nóng")]
        Hot,

        [Display(Name = "Trời mát")]
        Cool,

        [Display(Name = "Trời mưa")]
        Rainy
    }
}
