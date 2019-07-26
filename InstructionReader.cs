using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolDec {
	public class InstructionReader {
		private Stream Stream;
		private Queue<Instruction> AlreadyReadInstructions;

		public InstructionReader(Stream stream) {
			this.Stream = stream;
			this.AlreadyReadInstructions = new Queue<Instruction>();
		}

		public Instruction ReadInstructionBlock() {
			Instruction instr = new Instruction(InstructionType.Block);
			Instruction subInstr;
			while ((subInstr = this.ReadInstruction()).InstructionType != InstructionType.End) {
				instr.Children.Add(subInstr);
			}
			
			return instr;
		}

		public Instruction ReadInstruction() {
			if (this.AlreadyReadInstructions.Any()) {
				return this.AlreadyReadInstructions.Dequeue();
			}

			int cmd;
			long pos = this.Stream.Position;
			if ((cmd = this.Stream.ReadByte()) < 0) {
				return Instruction.Invalid;
			}

			Instruction instr = Instruction.Invalid;
			Instruction subInstr;

			switch (cmd & 0xF0) {
			case 0x00:
				instr = this.ReadConstant(cmd);
				break;
			case 0x10:
			case 0x20:
				instr = this.ReadMemory(cmd);
				break;
			case 0x30:
				this.ReadScriptOffset(cmd);
				instr = new Instruction(InstructionType.Expression);
				while (!(subInstr = this.ReadInstruction()).IsExpressionEnd()) {
					instr.Children.Add(subInstr);
				}
				break;
			case 0x40:
				if ((cmd & 0xF) == 0xF) {
					instr = new Instruction(InstructionType.Parameter, 0xF + this.Stream.ReadByte());
				} else {
					instr = new Instruction(InstructionType.Parameter, cmd & 0xF);
				}
				break;
			case 0x50:
				this.ReadScriptOffset(cmd);
				instr = this.ReadKeyword();
				break;
			case 0x60:
				this.ReadScriptOffset(cmd);
				instr = this.ReadControl();
				break;
			case 0x70:
				this.ReadScriptOffset(cmd);
				instr = this.ReadCall();
				break;
			case 0x80:
				this.ReadScriptOffset(cmd);
				instr = new Instruction(InstructionType.Block);
				while ((subInstr = this.ReadInstruction()).InstructionType != InstructionType.End) {
					instr.Children.Add(subInstr);
				}
				break;
			case 0x90:
				instr = new Instruction(InstructionType.Variable, cmd & 0xF);
				break;
			case 0xA0:
			case 0xB0:
				instr = new Instruction(InstructionType.Operator, cmd & 0x1F);
				break;
			case 0xC0:
			case 0xD0:
			case 0xE0:
			case 0xF0:
				instr = this.ReadConstant(cmd);
				break;
			}

			instr.Address = pos;
			return instr;
		}

		protected int ReadScriptOffset(int cmd) {
			switch (cmd & 0xF) {
			case 0xD:
				return this.Stream.ReadByte();
			case 0xE:
				return
					(this.Stream.ReadByte()) |
					(this.Stream.ReadByte() << 8);
			case 0xF:
				return
					(this.Stream.ReadByte()) |
					(this.Stream.ReadByte() << 8) |
					(this.Stream.ReadByte() << 16);
			default:
				return cmd & 0xF;
			}
		}

		protected Instruction ReadConstant(int cmd) {
			int val = 0;
			DataType type;
			if ((cmd & 0xF0) == 0) {
				switch (cmd) {
				case 0:
					return new Instruction(InstructionType.End);
				case 1:
					type = DataType.Int16;
					val  = (short)(
						(this.Stream.ReadByte()) |
						(this.Stream.ReadByte() << 8)
					);
					break;
				case 2:
				case 3:
				case 4:
					type = DataType.UInt8;
					val  = (byte)(
						(this.Stream.ReadByte())
					);
					break;
				case 6:
				case 8:
					type = DataType.UInt16;
					val  = (ushort)(
						(this.Stream.ReadByte()) |
						(this.Stream.ReadByte() << 8)
					);
					break;
				case 9:
				case 10:
				case 13:
					type = DataType.Int32;
					val  = (int)(
						(this.Stream.ReadByte()) |
						(this.Stream.ReadByte() << 8) |
						(this.Stream.ReadByte() << 16) |
						(this.Stream.ReadByte() << 24)
					);
					break;
				case 7:
				case 14:
					throw new Exception("String/byte array not handled yet");
				default:
					throw new Exception("Unknown data type " + cmd);
				}
			} else {
				type = DataType.Int32;
				val = (cmd & 0x3F) - 1;
			}
			return new Instruction(InstructionType.Constant, type, val);
		}

		protected Instruction ReadMemory(int cmd) {
			int addr;
			switch (this.Stream.ReadByte() & 0xF0) {
			case 0x80:
				addr = 0x203D800;
				break;
			case 0x10:
				addr = 0x203F000;
				break;
			default:
				addr = 0x203E800;
				break;
			}

			addr +=
				(this.Stream.ReadByte() << 8) |
				(this.Stream.ReadByte());

			DataType dataType = DataType.Void;
			switch (cmd & 0xF) {
			case 1:
			case 6:
				dataType = DataType.Int16;
				break;
			case 2:
			case 3:
				dataType = DataType.UInt8;
				break;
			case 4:
				dataType = DataType.Bool;
				break;
			case 8:
				dataType = DataType.UInt24;
				break;
			case 9:
				dataType = DataType.Int32;
				break;
			}

			Instruction instr = new Instruction();
			instr.DataType = dataType;
			instr.Value = addr;

			if ((cmd & 0xF0) == 0x20) {
				instr.InstructionType = InstructionType.MemoryIndexed;
				instr.Children.Add(this.ReadInstruction());
				instr.Children.Add(this.ReadInstruction());
			} else {
				instr.InstructionType = InstructionType.Memory;
			}

			return instr;
		}

		protected Instruction ReadKeyword() {
			int keyword = this.Stream.ReadByte();
			Instruction instr = new Instruction(InstructionType.Keyword, keyword);

			switch (keyword) {
			case (int)KeywordType.Else:
				instr.Children.Add(this.ReadInstruction());
				break;
			case (int)KeywordType.ElseIf:
				instr.Children.Add(this.ReadInstruction());
				instr.Children.Add(this.ReadInstruction());
				break;
			case (int)KeywordType.Keyword_6D:
				instr.Children.Add(this.ReadInstruction());
				break;
			default:
				throw new Exception("Unrecognized keyword 0x" + keyword.ToString("X"));
			}

			return instr;
		}

		protected Instruction ReadControl() {
			int tag =
				(this.Stream.ReadByte()) |
				(this.Stream.ReadByte() << 8);
			if ((this.Stream.ReadByte() & 0x80) != 0) {
				this.Stream.ReadByte();
			}

			Instruction instr = new Instruction(InstructionType.Control, tag);
			Instruction subInstr;

			switch (tag) {
			case (int)ControlType.If:
				instr.Children.Add(this.ReadInstruction());
				instr.Children.Add(this.ReadInstruction());
				while ((subInstr = this.ReadInstruction()).InstructionType != InstructionType.End) {
					instr.Children.Add(subInstr);
				}
				break;
			case (int)ControlType.Return:
				instr.Children.Add(this.ReadInstruction());
				break;
			case (int)ControlType.Ctrl_B745:
				instr.Children.Add(this.ReadInstruction());
				while ((subInstr = this.ReadInstruction()).InstructionType != InstructionType.End) {
					if (subInstr.InstructionType == InstructionType.Keyword && subInstr.Value == (int)KeywordType.Keyword_6D) {
						instr.Children.Add(subInstr.Children[0]);
					}
				}
				break;
			default:
				throw new Exception("Unrecognized control type 0x" + tag.ToString("X"));
			}

			return instr;
		}

		protected Instruction ReadCall() {
			int tag =
				(this.Stream.ReadByte()) |
				(this.Stream.ReadByte() << 8);

			Instruction instr = new Instruction(InstructionType.Call, tag);
			Instruction subInstr;
			while ((subInstr = this.ReadInstruction()).InstructionType != InstructionType.End) {
				instr.Children.Add(subInstr);
			}

			return instr;
		}
	}
}
