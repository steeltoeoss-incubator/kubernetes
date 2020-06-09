using System;
using System.Net;
using System.Net.Http;
using k8s;
using Microsoft.Rest.TransientFaultHandling;

namespace Steeltoe.Informers.KubernetesBase
{
    public static class Extensions
    {
        /// <summary>
        ///     Checks if the type of exception is the one that is temporary and will resolve itself over time.
        /// </summary>
        /// <param name="exception">Exception to check.</param>
        /// <returns>Return <see langword="true"/> if exception is transient, or <see langword="false"/> if it's not.</returns>
        public static bool IsTransient(this Exception exception)
        {
            if (exception is HttpRequestWithStatusException statusException)
            {
                return statusException.StatusCode >= HttpStatusCode.ServiceUnavailable || statusException.StatusCode == HttpStatusCode.RequestTimeout;
            }

            if (exception is HttpRequestException || exception is KubernetesException)
            {
                return true;
            }

            return false;
        }
        internal static string ToCamelCase(this string value)
        {
            // If there are 0 or 1 characters, just return the string.
            if (value == null || value.Length < 2)
            {
                return value;
            }

            // Split the string into words.
            var words = value.Split(
                new char[0],
                StringSplitOptions.RemoveEmptyEntries);

            // Combine the words.
            var result = words[0].ToLower();
            for (var i = 1; i < words.Length; i++)
            {
                result +=
                    words[i].Substring(0, 1).ToUpper() +
                    words[i].Substring(1);
            }

            return result;
        }
    }
}
