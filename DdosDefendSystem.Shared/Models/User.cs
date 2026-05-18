using System.ComponentModel.DataAnnotations;

namespace DdosDefendSystem.Shared.Models;

public class User
{
    [Key]
    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = "Admin";
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}