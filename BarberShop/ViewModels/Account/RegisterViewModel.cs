using System.ComponentModel.DataAnnotations;

namespace BarberShop.ViewModels.Account;

public class RegisterViewModel
{
    [Required(ErrorMessage = "O nome completo é obrigatório.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "O nome deve ter entre 3 e 100 caracteres.")]
    [Display(Name = "Nome Completo")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "O número de celular é obrigatório.")]
    [Display(Name = "Celular com DDD")]
    [RegularExpression(@"^(\+?55)?\s?\(?[1-9]{2}\)?\s?9?[0-9]{4}[-\s]?[0-9]{4}$", ErrorMessage = "Informe um celular válido com DDD (ex: (11) 98765-4321).")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "A senha é obrigatória.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "A senha deve ter no mínimo 6 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Senha")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "A confirmação de senha é obrigatória.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmar Senha")]
    [Compare("Password", ErrorMessage = "As senhas não conferem.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Range(typeof(bool), "true", "true", ErrorMessage = "Você deve aceitar os termos de uso para continuar.")]
    [Display(Name = "Aceito os Termos de Uso")]
    public bool AcceptTerms { get; set; }

    [Range(typeof(bool), "true", "true", ErrorMessage = "Você deve concordar com a política de privacidade.")]
    [Display(Name = "Aceito a Política de Privacidade")]
    public bool AcceptPrivacy { get; set; }

    [Display(Name = "Desejo receber confirmações e lembretes de agendamentos pelo WhatsApp")]
    public bool WhatsAppConsent { get; set; } = true;
}
