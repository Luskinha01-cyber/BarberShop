using System.ComponentModel.DataAnnotations;

namespace BarberShop.ViewModels.Account;

public class LoginViewModel
{
    [Required(ErrorMessage = "Informe seu número de celular ou e-mail de acesso.")]
    [Display(Name = "Celular ou E-mail")]
    public string Identifier { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe sua senha.")]
    [DataType(DataType.Password)]
    [Display(Name = "Senha")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Lembrar de mim")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
