namespace Task4.Services;

public class DuplicateEmailException : Exception
{
    public DuplicateEmailException() : base("Email is already registered.")
    {
    }
}
