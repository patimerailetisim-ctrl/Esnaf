using System;

namespace Esnaf.Core
{
    /// <summary>
    /// Beklenen hatalar için dönüş tipi (yetersiz nakit, dolu raf...). İstisna yalnızca programcı hatası içindir.
    /// default(Result) başarılı sayılır.
    /// </summary>
    public readonly struct Result
    {
        public string ErrorCode { get; }
        public string Message { get; }

        public bool IsSuccess
        {
            get { return ErrorCode == null; }
        }

        public bool IsFailure
        {
            get { return ErrorCode != null; }
        }

        private Result(string errorCode, string message)
        {
            ErrorCode = errorCode;
            Message = message;
        }

        public static Result Ok()
        {
            return new Result(null, null);
        }

        /// <param name="errorCode">Makine tarafından okunur kararlı kod, örn. "cash.insufficient".</param>
        /// <param name="message">İsteğe bağlı, geliştirici için açıklama (oyuncu metni değil).</param>
        public static Result Fail(string errorCode, string message = null)
        {
            if (string.IsNullOrEmpty(errorCode))
            {
                throw new ArgumentException("Error code is required.", nameof(errorCode));
            }

            return new Result(errorCode, message);
        }

        public override string ToString()
        {
            return IsSuccess ? "Ok" : "Fail(" + ErrorCode + ")";
        }
    }

    public readonly struct Result<T>
    {
        private readonly T _value;

        public string ErrorCode { get; }
        public string Message { get; }

        public bool IsSuccess
        {
            get { return ErrorCode == null; }
        }

        public bool IsFailure
        {
            get { return ErrorCode != null; }
        }

        /// <summary>Başarısız sonuçta okunursa InvalidOperationException fırlatır.</summary>
        public T Value
        {
            get
            {
                if (IsFailure)
                {
                    throw new InvalidOperationException("Cannot read Value of a failed result: " + ErrorCode);
                }

                return _value;
            }
        }

        private Result(T value, string errorCode, string message)
        {
            _value = value;
            ErrorCode = errorCode;
            Message = message;
        }

        public static Result<T> Ok(T value)
        {
            return new Result<T>(value, null, null);
        }

        public static Result<T> Fail(string errorCode, string message = null)
        {
            if (string.IsNullOrEmpty(errorCode))
            {
                throw new ArgumentException("Error code is required.", nameof(errorCode));
            }

            return new Result<T>(default(T), errorCode, message);
        }

        public static implicit operator Result(Result<T> result)
        {
            return result.IsSuccess ? Result.Ok() : Result.Fail(result.ErrorCode, result.Message);
        }

        public override string ToString()
        {
            return IsSuccess ? "Ok(" + _value + ")" : "Fail(" + ErrorCode + ")";
        }
    }
}
