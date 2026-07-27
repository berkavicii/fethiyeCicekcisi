namespace FethiyeCicekcisi.Core.Entities;

public class Address : BaseEntity
{
    public string? UserId { get; set; }
    public AppUser? User { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string? ZipCode { get; set; }
    public string Country { get; set; } = "Türkiye";
    public bool IsDefault { get; set; } = false;
}
