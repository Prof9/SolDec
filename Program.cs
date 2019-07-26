using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolDec {
	class Program {
		static void Main(string[] args) {
			using (FileStream fs = new FileStream(args[0], FileMode.Open, FileAccess.Read, FileShare.Read)) {
				fs.Position = int.Parse(args[1].Substring(2), NumberStyles.HexNumber);
				InstructionReader reader = new InstructionReader(fs);

				try {
					Instruction instr;
					while ((instr = reader.ReadInstruction()).InstructionType != InstructionType.End) {
						Console.WriteLine(instr);
						if (instr.InstructionType == InstructionType.Block) {
							break;
						}
					}
				} catch { }
			}

			Console.ReadKey();
		}
	}
}
