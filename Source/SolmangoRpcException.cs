// Copyright Siamango

using System;

namespace SolmangoNET.Exceptions;

public class SolmangoRpcException : Exception
{
    public string Reason { get; private set; }

    public int Code { get; private set; }

    public SolmangoRpcException(string reason, int code) : base($"Error[{code}]: {reason}")
    {
        Reason = reason;
        Code = code;
    }
}