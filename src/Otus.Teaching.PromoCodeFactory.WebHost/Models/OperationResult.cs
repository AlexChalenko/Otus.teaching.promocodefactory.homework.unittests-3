using Microsoft.AspNetCore.Http;
using System.Net;

namespace Otus.Teaching.PromoCodeFactory.WebHost.Models
{
    public class OperationResult<T>
    {
        public bool Success { get; private set; }
        public string ErrorMessage { get; private set; }
        public T Data { get; private set; }
        public HttpStatusCode StatusCode { get; }

        private OperationResult(bool success, string errorMessage, HttpStatusCode statusCode, T data)
        {
            Success = success;
            ErrorMessage = errorMessage;
            Data = data;
            StatusCode = statusCode;
        }

        public static OperationResult<T> Ok(T data) => new OperationResult<T>(true, null, HttpStatusCode.OK, data);

        public static OperationResult<T> NotFound(string message) => new OperationResult<T>(false, message, HttpStatusCode.NotFound, default);

        public static OperationResult<T> BadRequest(string message) => new OperationResult<T>(false, message, HttpStatusCode.BadRequest, default);
    }

}
