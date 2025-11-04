namespace CinemaS.Models
{
    public static class SD
    {
        public const string Role_Customer = "Customer";
        public const string Role_Company = "Company";
        public const string Role_Admin = "Admin";
        public const string Role_Employee = "Employee";

        // Nếu sau này bạn muốn thêm role, chỉ thêm hằng số ở đây là đủ.
        public static readonly string[] AllRoles = new[]
        {
            Role_Customer, Role_Company, Role_Admin, Role_Employee
        };
    }
}
