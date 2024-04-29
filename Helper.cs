using System.IO;
using System.Text;
using Zorcher.ProjectC.ProjectCInterface.Enums;
using Zorcher.ProjectC.ProjectCInterface.Plugin;

namespace Zandronum {
    internal static class Helper {
        private const char TextcolorEscape = '\x1c';

        // This is called to get a string from a reader
        internal static bool ReadNullTerminatedStringFromBinaryReader( BinaryReader br, string description, out string result ) {
            // Initialize
            StringBuilder sb = new StringBuilder( 16 );

            // Verify we are within bounds of the BinaryReader
            if ( br != null && br.BaseStream.Position + 1 <= br.BaseStream.Length ) {
                char schar = br.ReadChar();

                // Verify within bounds and not end of string
                while ( schar != '\0' ) {
                    // Append and take in the next character
                    sb.Append( schar );

                    // Verify still within bounds
                    if ( br.BaseStream.Position + 1 <= br.BaseStream.Length )
                        schar = br.ReadChar();
                    else break; // This is because null terminator at end of stream cannot be read
                }

                // Success
                result = sb.ToString();
                return true;
            }

            // Null or nothing to read
            result = null;
            return false;
        }

        // This is called to strip color codes from player names
        internal static string StripColorCodes( string s ) {
            // Get color code
            int index = s.IndexOf( TextcolorEscape );

            // Verify we still have characters to strip
            while ( index >= 0 ) {
                // Strip and find the next one
                s = s.Substring( 0, index ) + s.Substring( index + 2 );
                index = s.IndexOf( TextcolorEscape );
            }

            return s;
        }

        // This is called to parse a byte from the BinaryReader
        internal static bool ReadByteFromBinaryReader( Plugin plugin, BinaryReader br, string description, out byte result ) {
            // Verify within bounds of byte value
            if ( br != null && br.BaseStream.Position + 1 <= br.BaseStream.Length ) {
                result = br.ReadByte();
                return true;
            }

            // Error, beyond end of stream 1 byte
            plugin.General.LobbyChatWriteLine(
                "Incomplete packet received from Zandronum server while attempting to read byte: " + description,
                CHATCOLOR.WARNING, true, true );

            result = 0;
            return false;
        }

        // This is called to parse a short from the BinaryReader
        internal static bool ReadShortFromBinaryReader( Plugin plugin, BinaryReader br, string description, out short result ) {
            // Verify within bounds of short value
            if ( br != null && br.BaseStream.Position + 2 <= br.BaseStream.Length ) {
                result = br.ReadInt16();
                return true;
            }

            // Error, beyond end of stream 2 bytes
            plugin.General.LobbyChatWriteLine(
                "Incomplete packet received from Zandronum server while attempting to read short: " + description,
                CHATCOLOR.WARNING, true, true );

            result = 0;
            return false;
        }

        // This is called to parse an int from the BinaryReader
        internal static bool ReadIntFromBinaryReader( Plugin plugin, BinaryReader br, string description, out int result ) {
            // Verify within bounds of integer
            if ( br != null && br.BaseStream.Position + 4 <= br.BaseStream.Length ) {
                result = br.ReadInt32();
                return true;
            }

            // Error, beyond end of stream 4 bytes
            plugin.General.LobbyChatWriteLine(
                "Incomplete packet received from Zandronum server while attempting to read integer: " + description,
                CHATCOLOR.WARNING, true, true );

            result = 0;
            return false;
        }

        // This is called to parse a float from the BinaryReader
        internal static bool ReadSingleFromBinaryReader( Plugin plugin, BinaryReader br, string description, out float result ) {
            // Verify within bounds of float
            if ( br != null && br.BaseStream.Position + 4 <= br.BaseStream.Length ) {
                result = br.ReadSingle();
                return true;
            }

            // Error, beyond end of stream 4 bytes
            plugin.General.LobbyChatWriteLine(
                "Incomplete packet received from Zandronum server while attempting to read float: " + description,
                CHATCOLOR.WARNING, true, true );

            result = 0;
            return false;
        }
    }
}
