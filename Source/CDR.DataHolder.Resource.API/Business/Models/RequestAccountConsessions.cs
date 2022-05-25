using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CDR.DataHolder.Resource.API.Business.Filters;
using Microsoft.AspNetCore.Mvc;

namespace CDR.DataHolder.Resource.API.Business.Models
{
    public class RequestAccountConsessions : IValidatableObject
    {
        [FromRoute(Name = "accountId")]
        public string AccountId { get; set; }

        public Guid CustomerId { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();
            
            if (string.IsNullOrEmpty(this.AccountId))
                results.Add(new ValidationResult("Invalid account id.", new List<string> { "accountId" }));

            return results;
        }
    }
}
