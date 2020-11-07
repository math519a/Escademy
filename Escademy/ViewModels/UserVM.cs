using Escademy.Dal;
using System.Collections.Generic;

namespace Escademy.ViewModels
{
    public class UserVM
    {
        public int Id { get; set; }
        public int Level { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Picture { get; set; }
        public string Description { get; set; }
        public string Country { get; set; }

        public List<esc_profileTrophies> Trophies { get; set; }
        public List<esc_profilelanguages> Languages { get; set; }
    }
}