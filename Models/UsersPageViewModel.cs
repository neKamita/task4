namespace Task4.Models;

public class UsersPageViewModel
{
    public List<User> Users { get; set; } = [];
    public int CurrentUserId { get; set; }
    public bool HasUnverifiedUsers { get; set; }
}
