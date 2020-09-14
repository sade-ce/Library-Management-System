﻿using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;

namespace Library.Models.Account
{
    public class RegisterViewModel
    {
        [Required]
        [MinLength(2)]
        [MaxLength(50)]
        [Display(Name = "First name")]
        public string FirstName { get; set; }

        [Required]
        [MinLength(2)]
        [MaxLength(50)]
        [Display(Name = "Last name")]
        public string LastName { get; set; }

        [Required] 
        [EmailAddress] 
        [MaxLength(50)] 
        [Display(Name = "Email address")]
        [Remote(action: "IsEmailInUse", controller: "Account")]
        public string Email { get; set; }

        [Required]  
        [MinLength(8)] 
        [MaxLength(50)]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The confirmation password does not match the password")]
        public string ConfirmPassword { get; set; }
    }
}