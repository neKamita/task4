using System.ComponentModel.DataAnnotations;

namespace Task4.Models;

public class RegisterViewModel
{
    [Required]
    [StringLength(120)]
    public string Name { get; set; } = "";

    [Required]
    [EmailAddress]
    [StringLength(320)]
    public string Email { get; set; } = "";

    [Required]
    [StringLength(128)]
    public string Password { get; set; } = "";
}
