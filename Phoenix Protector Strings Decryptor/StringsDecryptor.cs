using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Console = Colorful.Console;

namespace Phoenix_Protector_Strings_Decryptor
{
    public static class StringsDecryptor
    {
        public static int DecryptStrings(ModuleDefMD module)
        {
            MethodDef decryptionMethod = FindDecryptionMethod(module);
            if (decryptionMethod == null)
                return 0;

            var decryptedCount = 0;
            var stack = new Stack();
            foreach (TypeDef type in module.Types.Where(t => t.HasMethods))
            {
                foreach (MethodDef method in type.Methods.Where(m => m.HasBody && m.Body.HasInstructions))
                {
                    IList<Instruction> instr = method.Body.Instructions;
                    for (var i = 0; i < instr.Count; i++)
                    {
                        if (instr[i].OpCode == OpCodes.Ldstr)
                        {
                            var encryptedString = new Tuple<string, int>((string) instr[i].Operand, i);
                            stack.Push(encryptedString);
                        }
                        else if (instr[i].OpCode == OpCodes.Call && instr[i].Operand is MethodDef callMethod &&
                                 callMethod == decryptionMethod)
                        {
                            (string encryptedString, int index) = (Tuple<string, int>) stack.Pop();
                            string decryptedString = Decrypt(encryptedString);
                            instr[index].OpCode = OpCodes.Nop;
                            instr[i] = Instruction.Create(OpCodes.Ldstr, decryptedString);
                            
                            Console.WriteLine($"[$] Decrypted {decryptedString}", Color.Purple);
                            decryptedCount++;
                        }
                    }
                }
            }

            return decryptedCount;
        }

        private static MethodDef FindDecryptionMethod(ModuleDef module)
        {
            foreach (TypeDef type in module.Types.Where(t => t.HasMethods))
            {
                foreach (MethodDef method in type.Methods.Where(m => m.HasBody && m.Body.HasInstructions))
                {
                    if (!string.IsNullOrWhiteSpace(method.DeclaringType.Namespace) ||
                        method.ReturnType != module.CorLibTypes.String || method.GetParamCount() != 1 ||
                        method.Parameters[0].Type != module.CorLibTypes.String) continue;
                    // Stores all the calls in a list to avoid loop.
                    List<Instruction> callInstrs = method.Body.Instructions.Where(i => i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt).ToList();
                    List<Instruction> getLength = callInstrs.FindAll(o => o.Operand.ToString().Contains("get_Length") &&
                                                                          o.Operand is MemberRef getLengthCall &&
                                                                          getLengthCall.ReturnType == module.CorLibTypes.Int32 &&
                                                                          !getLengthCall.HasParams());
                    List<Instruction> getChars = callInstrs.FindAll(o => o.Operand.ToString().Contains("get_Chars") &&
                                                                         o.Operand is MemberRef getCharsCall &&
                                                                         getCharsCall.ReturnType == module.CorLibTypes.Char &&
                                                                         getCharsCall.HasParams() &&
                                                                         getCharsCall.GetParamCount() == 1 &&
                                                                         getCharsCall.GetParams()[0].ToTypeDefOrRefSig() == module.CorLibTypes.Int32);
                    List<Instruction> intern = callInstrs.FindAll(o => o.Operand.ToString().Contains("Intern") &&
                                                                       o.Operand is MemberRef internCall &&
                                                                       internCall.ReturnType == module.CorLibTypes.String &&
                                                                       internCall.HasParams() &&
                                                                       internCall.GetParamCount() == 1 &&
                                                                       internCall.GetParams()[0].ToTypeDefOrRefSig() == module.CorLibTypes.String);
                    // If instruction found, then we're almost sure it's the correct decryption method.
                    if (getLength.Count != 1 || getChars.Count != 1 || intern.Count != 1) continue;
                    
                    return method;
                }
            }

            return null;
        }

        private static string Decrypt(string encryptedString)
        {
            var array = new char[encryptedString.Length];
            for (var i = 0; i < array.Length; i++)
                array[i] = (char)((byte)(encryptedString[i] >> 8 ^ i) << 8 | (byte)(encryptedString[i] ^ encryptedString.Length - i));
            return string.Intern(new string(array));
        }
    }
}