// Importam namespace-ul pentru DataAnnotations.
// DataAnnotations = atribute precum [Required], [EmailAddress], [Compare]
// Ele sunt folosite pentru VALIDAREA formularului.
using System.ComponentModel.DataAnnotations;

namespace TaskManager.Models
{
    // Aceasta clasa este un ViewModel.
    // ViewModel = o clasa intermediara intre View (formular) si Controller.
    // CONTINE DOAR DATE, NU logica, NU acces la baza de date.
    public class RegisterViewModel
    {
        // [Required] inseamna ca acest camp este OBLIGATORIU.
        // Daca utilizatorul nu completeaza FullName,
        // formularul nu se trimite si apare mesaj de eroare.
        [Required]
        public string FullName { get; set; } = "";
        // Initializam cu "" ca sa evitam valori null.

        // [Required] -> emailul trebuie completat.
        // [EmailAddress] -> ASP.NET verifica daca textul
        // are format valid de email (ex: ceva@domeniu.com).
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        // [Required] -> parola trebuie completata.
        // [DataType(DataType.Password)] spune Razor-ului
        // sa afiseze input-ul ca parola (●●●●).
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        // [Required] -> campul este obligatoriu.
        // [Compare(nameof(Password))] inseamna:
        // "ConfirmPassword trebuie sa fie IDENTICA cu Password".
        // Daca nu sunt la fel, apare eroare de validare.
        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password))]
        public string ConfirmPassword { get; set; } = "";
    }
}
