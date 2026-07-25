using System;
using System.Linq;
using FluentValidation.Validators;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Validation.Paths
{
    public class SeriesExistsValidator : PropertyValidator
    {
        private readonly ISeriesService _seriesService;

        public SeriesExistsValidator(ISeriesService seriesService)
        {
            _seriesService = seriesService;
        }

        protected override string GetDefaultMessageTemplate() => "This series has already been added";

        protected override bool IsValid(PropertyValidatorContext context)
        {
            if (context.PropertyValue == null)
            {
                return true;
            }

            var tvdbId = Convert.ToInt32(context.PropertyValue.ToString());

            // A TVDB ID can be added more than once as long as each copy is a distinct edition.
            var editionName = SeriesEditions.NormalizeEditionName((context.InstanceToValidate as ISeriesEditionIdentity)?.EditionName);

            return !_seriesService.AllSeriesEditions().TryGetValue(tvdbId, out var existingEditions) ||
                   !existingEditions.Any(e => SeriesEditions.SameEdition(e, editionName));
        }
    }
}
