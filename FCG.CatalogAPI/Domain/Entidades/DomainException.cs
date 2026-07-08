namespace FCG.CatalogAPI.Domain.Entidades;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}