using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PopFree.Pop3
{
    internal sealed class PopCommandResponse
    {
        private const string RESPONSE_OK = "+OK";
        //private const string RESPONSE_ERR="-ERR";

        private PopCommandResponse( string response )
        {
            RawValue = string.IsNullOrEmpty( response ) ? "POP server sent an empty response." : response;
            Success = IsResponseSuccess( response );
            Parameters = new ReadOnlyCollection<string>( ExtractParameters( response ) );
        }

        static PopCommandResponse()
        {
            NullResponse = new PopCommandResponse( null );
        }

        public static PopCommandResponse FromRawResponseString( string response )
        {
            return new PopCommandResponse( response );
        }

        public static PopCommandResponse NullResponse { get; private set; }

        private static string[] ExtractParameters( string input )
        {
            if( !string.IsNullOrEmpty( input ) )
            {
                string[] temp = input.Split( ' ' );
                if( temp.Length > 1 )
                {
                    string[] retStringArray = new string[temp.Length - 1];
                    Array.Copy( temp, 1, retStringArray, 0, temp.Length - 1 );
                    return retStringArray;
                }
                return new string[0];
            }
            return new string[0];
        }

        internal static bool IsResponseSuccess( string response )
        {
            if( !string.IsNullOrEmpty( response ) && response.Length >= 3 )
                return response.Trim().StartsWith( RESPONSE_OK );
            return false;
        }

        public bool Success { get; private set; }
        public string RawValue { get; private set; }
        public ReadOnlyCollection<string> Parameters { get; private set; }
        public IList<int> IntValues()
        {
            List<int> values = new List<int>( Parameters.Count );
            foreach( string param in Parameters )
            {
                int result;
                values.Add( int.TryParse( Parameters[0], out result ) ? result : -1 );
            }
            return values;
        }

        /// <summary>
        /// Throws a PopServerResponseErrException when called if the value of Success was false.
        /// </summary>
        internal void Throw()
        {
            if( !this.Success )
                throw new PopServerResponseErrException( this.RawValue );
        }

        internal PopServerResponseErrException ToException()
        {
            if( !this.Success )
                return new PopServerResponseErrException( this.RawValue );
            return null;
        }
    }
}
