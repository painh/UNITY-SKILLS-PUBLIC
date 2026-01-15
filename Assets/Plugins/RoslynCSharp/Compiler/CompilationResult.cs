using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;

namespace RoslynCSharp.Compiler
{
    /// <summary>
    /// Represents the result of a compilation operation.
    /// </summary>
    public class CompilationResult
    {
        private bool _success;
        private List<CompilationError> _errors;

        public bool Success => _success;
        public IReadOnlyList<CompilationError> Errors => _errors;

        internal CompilationResult(EmitResult emitResult)
        {
            _success = emitResult.Success;
            _errors = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error || d.Severity == DiagnosticSeverity.Warning)
                .Select(d => new CompilationError(d))
                .ToList();
        }

        internal CompilationResult(Exception exception)
        {
            _success = false;
            _errors = new List<CompilationError>
            {
                new CompilationError(exception)
            };
        }
    }

    /// <summary>
    /// Represents a compilation error or warning.
    /// </summary>
    public class CompilationError
    {
        private string _message;
        private string _errorCode;
        private bool _isError;
        private bool _isWarning;
        private int _line;
        private int _column;

        public string Message => _message;
        public string ErrorCode => _errorCode;
        public bool IsError => _isError;
        public bool IsWarning => _isWarning;
        public int Line => _line;
        public int Column => _column;

        internal CompilationError(Diagnostic diagnostic)
        {
            _message = diagnostic.GetMessage();
            _errorCode = diagnostic.Id;
            _isError = diagnostic.Severity == DiagnosticSeverity.Error;
            _isWarning = diagnostic.Severity == DiagnosticSeverity.Warning;

            var lineSpan = diagnostic.Location.GetLineSpan();
            _line = lineSpan.StartLinePosition.Line + 1;
            _column = lineSpan.StartLinePosition.Character + 1;
        }

        internal CompilationError(Exception exception)
        {
            _message = exception.Message;
            _errorCode = "EX0001";
            _isError = true;
            _isWarning = false;
            _line = 0;
            _column = 0;
        }

        public override string ToString()
        {
            if (_line > 0)
            {
                return $"({_line},{_column}): {(_isError ? "error" : "warning")} {_errorCode}: {_message}";
            }
            return $"{(_isError ? "error" : "warning")} {_errorCode}: {_message}";
        }
    }
}
