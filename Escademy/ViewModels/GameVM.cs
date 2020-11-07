using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Escademy.ViewModels
{
    public class GameVM
    {
        public int Id { get; set; }
        public int GameId { get; set; }
        public int AccountId { get; set; }
        public string Name { get; set; }
        public string Rating { get; set; }
    }
}