using System.Globalization;
using System.Windows.Controls;
using System;
using System.Windows.Data;
using System.Collections.ObjectModel;
using System.Windows;

namespace WebDAVDrive.UI
{
    /// <summary>
    /// Login validation class
    /// </summary>
    class LoginValidationRule : ValidationRule 
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var str = value as string;
            if (string.IsNullOrEmpty(str))
            {
                return new ValidationResult(false, Localization.Resources.LoginError);
            }
            return new ValidationResult(true, null);
        }
    }

    /// <summary>
    /// Url validation class
    /// </summary>
    class UrlValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var str = value as string;
            if (string.IsNullOrEmpty(str) || !Uri.IsWellFormedUriString(str, UriKind.RelativeOrAbsolute))
            {
                return new ValidationResult(false, Localization.Resources.URLError);
            }
            return new ValidationResult(true, null);
        }
    }

    /// <summary>
    /// This class used to convert validation errors to text and display on form as red text boxes
    /// </summary>
    class ErrorCollectionToVisibility : IValueConverter
    {
        /// <summary>
        /// This variable defines if field was checked before.
        /// We need this to hide error message at start of applocation, when field is empty 
        /// </summary>
        private bool firstCheck = false;
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var collection = value as ReadOnlyCollection<ValidationError>;
            if (collection != null && collection.Count > 0)
            {
                if (firstCheck)
                    return Visibility.Visible;
                else 
                {
                    firstCheck = true;
                    return Visibility.Collapsed;
                }
            }
            else
                return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return new object();
        }
    }
}
