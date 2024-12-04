using System.ComponentModel.DataAnnotations;

namespace Client;

public class RegisterDTO
{
    [Required(ErrorMessage = "Email обязателен.")]
    [EmailAddress(ErrorMessage = "Некорректный email.")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Пароль обязателен.")]
    [MinLength(6, ErrorMessage = "Пароль должен содержать минимум 6 символов.")]
    public string Password { get; set; }
}
