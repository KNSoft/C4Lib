/* Too hacky, for KNSoft internal use only */

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace KNSoft.C4Lib.CodeHelper;

public class Cpp
{
    public class CodeFragment
    {
        public static readonly String PragmaOnce = "#pragma once";
        public static readonly String ExternCStart = "EXTERN_C_START";
        public static readonly String ExternCEnd = "EXTERN_C_END";
        public static readonly String AutoGenerateFileComment = "/* This file was auto-generated, do not change this file manually */";
    }

    public static StreamWriter CreateFile(String Path, Boolean Append, Boolean HasUtf8Bom)
    {
        return new(Path, Append, new UTF8Encoding(HasUtf8Bom), 4096);
    }

    public static StreamWriter CreateOutputFile(String Path)
    {
        return CreateFile(Path, false, true);
    }

    public static void OutputWithNewLine(StreamWriter Stream, String Code)
    {
        Stream.WriteLine(Code);
        Stream.WriteLine();
    }

    public class Parameter
    {
        public List<String> Prefixes = [];
        public String Type = String.Empty;
        public UInt16? FixedSize = null;
        public String Name = String.Empty;
        public List<String> Comments = [];
        public String LineComment = String.Empty;
    }

    public class Function
    {
        public List<String> Prefixes = [];
        public String Name = String.Empty;
        public List<Parameter> Parameters = [];
        public String Content = String.Empty;
    }

    public class CodeResolver
    {
        public static Boolean IsFunctionDeclarationStart(String Line)
        {
            return String.IsNullOrWhiteSpace(Line) ||
                Line.TrimStart().StartsWith(";") ||
                Line.TrimStart().StartsWith("#") ||
                Line.TrimStart().StartsWith("//") ||
                Line.TrimEnd().EndsWith("*/");
        }

        public static Boolean IsFunctionDeclarationEnd(String Line)
        {
            Int32 i = Line.IndexOf("//");
            if (i != -1)
            {
                Line = Line[..i];
            }
            Line = Line.TrimEnd();
            if (!Line.EndsWith(";"))
            {
                return false;
            }
            Line = Line[..(Line.Length - 1)].TrimEnd();
            return String.IsNullOrEmpty(Line) || Line.EndsWith(")");
        }

        private static String FetchToken(ref String Token)
        {
            String Temp = Token;
            Token = String.Empty;
            return Temp;
        }

        private static void AddParameterToFunction(Int32 LineNum, String[] LineComments, ref Function Func, ref Parameter Param)
        {
            LineNum -= 2;
            if (LineNum >= 0 && LineNum < LineComments.Length && !String.IsNullOrEmpty(LineComments[LineNum]))
            {
                Param.LineComment = LineComments[LineNum];
            }
            if (String.IsNullOrEmpty(Param.Type))
            {
                Param.Type = Param.Name;
                Param.Name = String.Empty;
            }
            if (Param.Name.StartsWith("*"))
            {
                Param.Name = Param.Name[1..];
                Param.Type += '*';
            }
            if (Param.Type == "*")
            {
                Param.Type = Param.Prefixes[0] + '*';
                Param.Prefixes.RemoveAt(0);
            }
            if (Param.Type == "VOID" || Param.Type == "void")
            {
                Param.FixedSize = 0;
            } else if (Param.Type == "POINT" ||
                       Param.Type == "ULONGLONG" || Param.Type == "ULONG64" ||
                       Param.Type == "UINT64" || Param.Type == "INT64" || Param.Type == "__int64" ||
                       Param.Type == "LONGLONG" || Param.Type == "LONG64" ||
                       Param.Type == "QWORD" || Param.Type == "DWORD64")
            {
                Param.FixedSize = 8;
            }
            Param.Prefixes.Reverse();
            Func.Parameters.Add(Param);
        }

        private static Boolean ApplyTokenToParameter(ref Parameter Param, ref String Token)
        {
            if (String.IsNullOrEmpty(Token))
            {
                return false;
            }
            if (Token.StartsWith("/*") && Token.EndsWith("*/"))
            {
                Param.Comments.Add(FetchToken(ref Token));
                return true;
            }

            if (String.IsNullOrEmpty(Param.Name))
            {
                Param.Name = FetchToken(ref Token);
            } else if (String.IsNullOrEmpty(Param.Type))
            {
                Param.Type = FetchToken(ref Token);
            } else
            {
                Param.Prefixes.Add(FetchToken(ref Token));
            }
            return true;
        }

        static public List<Function> GetFunctionsFromContent(String[] Content)
        {
            List<Function> Functions = [];

            /* Read file content */
            for (Int32 i = 0; i < Content.Length; i++)
            {
                Int32 iStart, iEnd;

                /* Find ';' */
                if (!IsFunctionDeclarationEnd(Content[i]))
                {
                    continue;
                }
                iEnd = i;

                /* Find beginning */
                for (iStart = iEnd - 1; iStart >= 0; iStart--)
                {
                    if (IsFunctionDeclarationStart(Content[iStart]))
                    {
                        break;
                    }
                }
                if (iStart < 0)
                {
                    iStart = 0;
                }

                /* Function found */
                try
                {
                    Function Func = new();
                    String[] Comments = new String[iEnd - iStart];
                    for (Int32 j = iStart; j <= iEnd; j++)
                    {
                        String LineContent = Content[j].Trim();
                        Int32 CommentIndex = LineContent.IndexOf("//");
                        if (CommentIndex != -1)
                        {
                            Comments[j - iStart] = LineContent[CommentIndex..LineContent.Length];
                            LineContent = LineContent[0..CommentIndex];
                        }
                        Func.Content += LineContent + '\n';
                    }
                    Func.Content = Func.Content[0..(Func.Content.Length - 1)];
                    Comments = [.. Comments.Reverse()];

                    /* Backward scan declaration, skip ';' */

                    Parameter Param = new();
                    Boolean ParameterEnds = false, InComment = false;
                    Int32 Brace = 0, LineNum = 0;
                    String Token = String.Empty;

                    for (Int32 j = Func.Content.Length - 2; j >= 0; j--)
                    {
                        /* Handle block comment */
                        if (!InComment && j >= 1 && Func.Content[j] == '/' && Func.Content[j - 1] == '*')
                        {
                            InComment = true;
                        } else if (InComment && j >= 1 && Func.Content[j] == '*' && Func.Content[j - 1] == '/')
                        {
                            Token = "/*" + Token;
                            j--;
                            InComment = false;
                            continue;
                        } else if (InComment)
                        {
                            Token = Func.Content[j] + Token;
                            continue;
                        }

                        if (Func.Content[j] == ')')
                        {
                            Brace++;
                            if (Brace == 1 && !ParameterEnds)
                            {
                                continue;
                            }
                        } else if (Func.Content[j] == '(')
                        {
                            if (Brace == 1 && !ParameterEnds)
                            {
                                ApplyTokenToParameter(ref Param, ref Token);
                                AddParameterToFunction(LineNum, Comments, ref Func, ref Param);
                                ParameterEnds = true;
                                Func.Parameters.Reverse();
                                Brace--;
                                continue;
                            }
                            Brace--;
                        } else if (Func.Content[j] == ',')
                        {
                            if (Brace == 1 && !ParameterEnds)
                            {
                                ApplyTokenToParameter(ref Param, ref Token);
                                AddParameterToFunction(LineNum, Comments, ref Func, ref Param);
                                Param = new();
                                continue;
                            }
                        } else if (Char.IsWhiteSpace(Func.Content[j]))
                        {
                            if (Func.Content[j] == '\n')
                            {
                                LineNum++;
                            }
                            if (String.IsNullOrEmpty(Token))
                            {
                                continue;
                            }
                            if (ParameterEnds)
                            {
                                if (String.IsNullOrEmpty(Func.Name))
                                {
                                    Func.Name = FetchToken(ref Token);
                                    continue;
                                } else if (Brace == 0)
                                {
                                    Func.Prefixes.Add(FetchToken(ref Token));
                                    continue;
                                }
                            } else if (Brace == 1)
                            {
                                ApplyTokenToParameter(ref Param, ref Token);
                                continue;
                            }
                        }
                        Token = Func.Content[j] + Token;
                    }
                    if (!String.IsNullOrEmpty(Token))
                    {
                        Func.Prefixes.Add(Token);
                    }
                    Func.Prefixes.Reverse();
                    Functions.Add(Func);
                } catch (Exception)
                {
                    Console.WriteLine("Unsupported content:");
                    for (Int32 j = iStart; j <= iEnd; j++)
                    {
                        Console.WriteLine(Content[j]);
                    }
                    Console.WriteLine();
                }
            }

            return Functions;
        }

        static public String[] FunctionToDeclaration(Function Func)
        {
            List<String> Content = [];

            foreach (String Prefix in Func.Prefixes)
            {
                Content.Add(Prefix);
            }
            Content.Add(Func.Name + '(');
            for (Int32 i = 0; i < Func.Parameters.Count; i++)
            {
                String Line = "    ";
                foreach (String Prefix in Func.Parameters[i].Prefixes)
                {
                    Line += Prefix + ' ';
                }
                Line += Func.Parameters[i].Type;
                if (!String.IsNullOrEmpty(Func.Parameters[i].Name))
                {
                    Line += ' ' + Func.Parameters[i].Name;
                }
                foreach (String Comment in Func.Parameters[i].Comments)
                {
                    Line += ' ' + Comment;
                }
                if (i != Func.Parameters.Count - 1)
                {
                    Line += ',';
                }
                if (!String.IsNullOrEmpty(Func.Parameters[i].LineComment))
                {
                    Line += ' ' + Func.Parameters[i].LineComment;
                }
                Content.Add(Line);
            }
            Content.Add("    );");

            return [.. Content];
        }
    }
}
