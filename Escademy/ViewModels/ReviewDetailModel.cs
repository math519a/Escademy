using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Escademy.ViewModels
{
    public class ReviewDetailModel
    {
        public int ReviewStars { get; set; }
        public string  ReviewComments { get; set; }
        public string Reviewer { get; set; }
        public int ReviewCount { get; set; }
        public DateTime ReviewDate { get; set; }
        public string ProfilePicture { get; set; }
        public double avrStar { get; set; }
    }
}