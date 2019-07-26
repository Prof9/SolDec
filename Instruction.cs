using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolDec {
	public enum InstructionType {
		Invalid,
		Constant,
		Memory,
		MemoryIndexed,
		Expression,
		Parameter,
		Keyword,
		Control,
		Call,
		Block,
		Variable,
		Operator,
		End,
	}

	public enum ControlType {
		If			= 0xD86,
		Switch		= 0x4A6F,
		Return		= 0xCD3A,
		Ctrl_B745	= 0xB745,
	}

	public enum KeywordType {
		Case		= 0x63,
		Default		= 0x64,
		Else		= 0x65,
		ElseIf		= 0x69,
		Keyword_6D	= 0x6D, 
	}

	public enum DataType {
		Void,
		Bool,
		UInt8,
		Int16,
		UInt16,
		UInt24,
		Int32
	}

	public class Instruction {
		public static readonly Instruction Invalid = new Instruction();
		private static readonly string[] DataTypeStrings = {
			"void",
			"bool",
			"uint8_t",
			"int16_t",
			"uint16_t",
			"uint24_t",
			"int32_t"
		};
		private static readonly string[] OperatorStrings = {
			 ";",  "-",  "!",  "~",  "+",  "-",  "*",  "/",
			 "%", "<<", ">>", "==", "!=",  "<", "<=",  ">",
			">=",  "|",  "&",  "^", "||", "&&",  "=",   "",
		};
		private static readonly byte[] OperatorPrecedence = {
			0,  2,  2,  2,  4,  4,  3,  3,
			3,  5,  5,  7,  7,  6,  6,  6,
			6, 10,  8,  9, 12, 11, 14, 16,
		};

		public long Address { get; set; }
		public InstructionType InstructionType { get; set; }
		public DataType DataType { get; set; }
		public IList<Instruction> Children { get; set; }
		public int Value { get; set; }
		public int Indent { get; set; }

		public Instruction()
			: this(InstructionType.Invalid, DataType.Void, 0) { }
		public Instruction(InstructionType type)
			: this(type, DataType.Void, 0) { }
		public Instruction(InstructionType type, int value)
			: this(type, DataType.Void, value) { }
		public Instruction(InstructionType instrType, DataType dataType, int value) {
			this.Address = 0;
			this.InstructionType = instrType;
			this.DataType = dataType;
			this.Value = value;
			this.Children = new List<Instruction>();
			this.Indent = 0;
		}

		public bool IsExpressionEnd()
			=> this.InstructionType == InstructionType.Operator
			&& this.Value == 0;

		public override string ToString() {
			string s;

			switch (this.InstructionType) {
			case InstructionType.Invalid:
				s = "?";
				break;
			case InstructionType.End:
				s = "}";
				break;
			case InstructionType.Constant:
				s = this.Value.ToString();
				break;
			case InstructionType.Memory:
				if (this.DataType == DataType.Bool) {
					s = "BIT(";
					s += "0x" + (this.Value / 8).ToString("X8");
					s += ", ";
					s += (this.Value % 8);
					s += ")";
				} else {
					s = "*(";
					s += "(" + DataTypeStrings[(int)this.DataType] + " *)";
					s += "0x" + this.Value.ToString("X");
					s += ")";
				}
				break;
			case InstructionType.MemoryIndexed:
				if (this.DataType == DataType.Bool) {
					s = "BIT(";
					s += "0x" + (this.Value / 8).ToString("X8");
					s += ", ";
					s += (this.Value % 8);
					s += ", ";
					s += this.Children[1].ToString();
					s += ")";
				} else {
					s = "(";
					s += "(" + DataTypeStrings[(int)this.DataType] + " *)";
					s += "0x" + this.Value.ToString("X");
					s += ")[";
					s += this.Children[1].ToString();
					s += "]";
				}
				break;
			case InstructionType.Expression:
				s = this.ExpressionToString();
				break;
			case InstructionType.Parameter:
				if (this.Value == 0) {
					s = "r";
				} else {
					s = "p" + (this.Value - 1);
				}
				break;
			case InstructionType.Keyword:
				s = this.KeywordToString();
				break;
			case InstructionType.Control:
				s = this.ControlToString();
				break;
			case InstructionType.Call:
				s = this.CallToString();
				break;
			case InstructionType.Block:
				s = "{" + Environment.NewLine + new string('\t', this.Indent + 1);
				s += this.ChildrenToString(0);
				s += Environment.NewLine + new string('\t', this.Indent) + "}";
				break;
			case InstructionType.Variable:
				s = "v" + this.Value;
				break;
			case InstructionType.Operator:
				s = OperatorStrings[this.Value];
				break;
			default:
				throw new Exception("Unexpected instruction type");
			}

			return s;
		}

		private string ExpressionToString() {
			Stack<string> dataStack = new Stack<string>(8);
			Stack<byte> precStack = new Stack<byte>(8);
			foreach (Instruction child in this.Children) {
				if (child.InstructionType == InstructionType.Operator) {
					if (child.Value == 0) {
						break;
					}
					string bStr  = dataStack.Pop();
					string aStr  = dataStack.Pop();
					byte   bPrec = precStack.Pop();
					byte   aPrec = precStack.Pop();
					byte   rPrec = OperatorPrecedence[child.Value];
					string op    = child.ToString();

					if (aPrec > rPrec) {
						aStr = "(" + aStr + ")";
					}
					if (bPrec > rPrec) {
						bStr = "(" + bStr + ")";
					}

					switch (child.Value) {
					case 1:
					case 2:
					case 3:
					case 23:
						// Unary operator
						dataStack.Push(op + bStr);
						break;
					case 22:
						// Assign
						dataStack.Push(aStr + " " + op + " " + bStr);
						break;
					default:
						// Binary operator
						dataStack.Push(aStr + " " + op + " " + bStr);
						break;
					}
					precStack.Push(rPrec);
				} else {
					if (child.InstructionType == InstructionType.Block &&
						child.Children.Count == 1) {
						dataStack.Push(child.Children[0].ToString());
					} else {
						dataStack.Push(child.ToString());
					}
					precStack.Push(0);
				}
			}
			return dataStack.Pop();
		}

		private string KeywordToString() {
			string s;
			switch (this.Value) {
			case (int)KeywordType.Case:
				s = "case ";
				s += this.Children[0].ToString();
				s += ":" + Environment.NewLine;
				s += this.ChildrenToString(1);
				s += "break;";
				break;
			case (int)KeywordType.Default:
				s = "default:" + Environment.NewLine;
				s += this.ChildrenToString(0);
				s += "break;";
				break;
			case (int)KeywordType.Else:
				this.Children[0].Indent = this.Indent;
				s = "else ";
				s += this.Children[0].ToString();
				break;
			case (int)KeywordType.ElseIf:
				this.Children[1].Indent = this.Indent;
				s = "else if (";
				s += this.Children[0].ToString();
				s += ") ";
				s += this.Children[1].ToString();
				break;
			default:
				throw new Exception("Unrecognized keyword type 0x" + this.Value.ToString("X"));
			}
			return s;
		}

		private string ControlToString() {
			string s;
			switch (this.Value) {
			case (int)ControlType.If:
				s = "if (";
				s += this.Children[0].ToString();
				s += ") ";
				s += this.ChildrenToString(1);
				break;
			case (int)ControlType.Return:
				s = "return ";
				s += this.Children[0].ToString();
				s += ";";
				break;
			case (int)ControlType.Ctrl_B745:
				s = "ctrl_0x" + this.Value.ToString("X") + "[";
				if (this.Children[0].InstructionType == InstructionType.Constant) {
					s += "0x" + this.Children[0].Value.ToString("X");
				} else {
					s += this.Children[0].ToString();
				}
				s += "](";
				s += this.ParamsToString(1);
				s += ")";
				break;
			default:
				throw new Exception("Unrecognized control type 0x" + this.Value.ToString("X"));
			}
			return s;
		}

		private string CallToString() {
			string s = "func_0x" + this.Value.ToString("X") + "(";
			s += this.ParamsToString(0);
			s += ")";

			return s;
		}

		private string ParamsToString(int skip) {
			string s = "";
			bool first = true;
			foreach (Instruction subInstr in this.Children.Skip(skip)) {
				if (!first) {
					s += ", ";
				}
				first = false;

				s += subInstr.ToString();
			}
			return s;
		}

		private string ChildrenToString(int skip) {
			string s = "";
			bool doNewLine = false;
			foreach (Instruction subInstr in this.Children.Skip(skip)) {
				if (subInstr.InstructionType == InstructionType.Keyword && (
					subInstr.Value == (int)KeywordType.Else ||
					subInstr.Value == (int)KeywordType.ElseIf
				)) {
					s += " ";
					doNewLine = false;
				}

				if (this.InstructionType == InstructionType.Control ||
					this.InstructionType == InstructionType.Keyword) {
					subInstr.Indent = this.Indent;
				} else {
					subInstr.Indent = this.Indent + 1;
				}
				if (doNewLine) {
					s += Environment.NewLine + new string('\t', this.Indent + 1);
				}
				doNewLine = true;

				s += subInstr.ToString();
				if (subInstr.InstructionType == InstructionType.Expression) {
					s += ";";
				}
			}
			return s;
		}
	}
}
