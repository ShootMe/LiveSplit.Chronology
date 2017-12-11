using System;
using System.Diagnostics;
using System.Drawing;
namespace LiveSplit.Chronology {
	public partial class SplitterMemory {
		private static ProgramPointer ChronologyStateManager = new ProgramPointer(true, new ProgramSignature(PointerVersion.V1, "8B15????????8BCFFF15????????5E5F5DC3|-16"));
		public Process Program { get; set; }
		public bool IsHooked { get; set; } = false;
		private DateTime lastHooked;

		public SplitterMemory() {
			lastHooked = DateTime.MinValue;
		}

		public bool IsLoading() {
			//ChronologyStateManager.globalStateGameInstance.gameSession.isLoading
			return ChronologyStateManager.Read<bool>(Program, 0x0, 0x24, 0x20);
		}
		public bool HasControl() {
			//ChronologyStateManager.globalStateGameInstance.gameSession.scene.controller.enabled
			return ChronologyStateManager.Read<bool>(Program, 0x0, 0x24, 0x24, 0x54, 0x18);
		}
		public PointF Position() {
			//ChronologyStateManager.globalStateGameInstance.gameSession.scene.controllable.previousPosition.x/y
			float x = ChronologyStateManager.Read<float>(Program, 0x0, 0x24, 0x24, 0x58, 0x44, 0x4);
			float y = ChronologyStateManager.Read<float>(Program, 0x0, 0x24, 0x24, 0x58, 0x44, 0x8);
			return new PointF(x, y);
		}
		public CharacterState CharacterState() {
			//ChronologyStateManager.globalStateGameInstance.gameSession.scene.controllable.state
			return (CharacterState)ChronologyStateManager.Read<int>(Program, 0x0, 0x24, 0x24, 0x58, 0x128);
		}
		public Level LevelID() {
			//ChronologyStateManager.globalStateGameInstance.gameSession.levelID
			return (Level)ChronologyStateManager.Read<int>(Program, 0x0, 0x24, 0x48);
		}
		public float Progress() {
			//ChronologyStateManager.globalStateGameInstance.gameSession.progress
			return ChronologyStateManager.Read<float>(Program, 0x0, 0x24, 0x50);
		}
		public bool HookProcess() {
			if ((Program == null || Program.HasExited) && DateTime.Now > lastHooked.AddSeconds(1)) {
				lastHooked = DateTime.Now;
				Process[] processes = Process.GetProcessesByName("Chronology");
				Program = processes.Length == 0 ? null : processes[0];
				IsHooked = true;
			}

			if (Program == null || Program.HasExited) {
				IsHooked = false;
			}

			return IsHooked;
		}
		public void Dispose() {
			if (Program != null) {
				Program.Dispose();
			}
		}
	}
	public enum PointerVersion {
		V1
	}
	public class ProgramSignature {
		public PointerVersion Version { get; set; }
		public string Signature { get; set; }
		public ProgramSignature(PointerVersion version, string signature) {
			Version = version;
			Signature = signature;
		}
		public override string ToString() {
			return Version.ToString() + " - " + Signature;
		}
	}
	public class ProgramPointer {
		private int lastID;
		private DateTime lastTry;
		private ProgramSignature[] signatures;
		private int[] offsets;
		public IntPtr Pointer { get; private set; }
		public PointerVersion Version { get; private set; }
		public bool AutoDeref { get; private set; }

		public ProgramPointer(bool autoDeref, params ProgramSignature[] signatures) {
			AutoDeref = autoDeref;
			this.signatures = signatures;
			lastID = -1;
			lastTry = DateTime.MinValue;
		}
		public ProgramPointer(bool autoDeref, params int[] offsets) {
			AutoDeref = autoDeref;
			this.offsets = offsets;
			lastID = -1;
			lastTry = DateTime.MinValue;
		}

		public T Read<T>(Process program, params int[] offsets) where T : struct {
			GetPointer(program);
			return program.Read<T>(Pointer, offsets);
		}
		public string Read(Process program, params int[] offsets) {
			GetPointer(program);
			IntPtr ptr = (IntPtr)program.Read<uint>(Pointer, offsets);
			return program.Read(ptr);
		}
		public byte[] ReadBytes(Process program, int length, params int[] offsets) {
			GetPointer(program);
			return program.Read(Pointer, length, offsets);
		}
		public void Write<T>(Process program, T value, params int[] offsets) where T : struct {
			GetPointer(program);
			program.Write<T>(Pointer, value, offsets);
		}
		public void Write(Process program, byte[] value, params int[] offsets) {
			GetPointer(program);
			program.Write(Pointer, value, offsets);
		}
		public IntPtr GetPointer(Process program) {
			if ((program?.HasExited).GetValueOrDefault(true)) {
				Pointer = IntPtr.Zero;
				lastID = -1;
				return Pointer;
			} else if (program.Id != lastID) {
				Pointer = IntPtr.Zero;
				lastID = program.Id;
			}

			if (Pointer == IntPtr.Zero && DateTime.Now > lastTry.AddSeconds(1)) {
				lastTry = DateTime.Now;

				Pointer = GetVersionedFunctionPointer(program);
				if (Pointer != IntPtr.Zero) {
					if (AutoDeref) {
						Pointer = (IntPtr)program.Read<uint>(Pointer);
					}
				}
			}
			return Pointer;
		}
		private IntPtr GetVersionedFunctionPointer(Process program) {
			if (signatures != null) {
				for (int i = 0; i < signatures.Length; i++) {
					ProgramSignature signature = signatures[i];

					IntPtr ptr = program.FindSignatures(signature.Signature)[0];
					if (ptr != IntPtr.Zero) {
						Version = signature.Version;
						return ptr;
					}
				}
			} else {
				IntPtr ptr = (IntPtr)program.Read<uint>(program.MainModule.BaseAddress, offsets);
				if (ptr != IntPtr.Zero) {
					return ptr;
				}
			}

			return IntPtr.Zero;
		}
	}
}