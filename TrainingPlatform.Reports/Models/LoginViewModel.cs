using System.ComponentModel.DataAnnotations;

namespace TrainingPlatform.Reports.Models
{
    public class LoginViewModel
    {
        // [Required] ensures the user cannot submit the form if this is empty
        [Required]
        public required string Email { get; set; }

        [Required]
        [DataType(DataType.Password)] // This hides the characters when typing
        public required string Password { get; set; }
    }
}