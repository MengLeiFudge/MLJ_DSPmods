# Issues - Fractionator Traits Implementation

## Date: 2026-03-03

### Issue 1: Dimensional Resosance boost formula needs base 1

**From TODO.md:**
- Success rate boost = 1 + √(x/120)`
- speed boost = 1 + √(x/60)

**Current code:**
```csharp
double successAdd = Math.Sqrt(x / 120.0);
double speedAdd = Math.Sqrt(x / 60.0);
```
**Correct formula (from TODO.md):**
```csharp
float successAdd = 1.0f + (float)Math.Sqrt(x / 120.0f);
float speedAdd = 1.0f + (float)Math.Sqrt(x / 60.0f);
```
The is **INCorrection 2** in UI display:
The boost values displayed in the info window show `1 + sqrt` but they should be showing the **actual boost** value.

**Recommendation:**
1. Fix the formula in ProcessManager.cs lines 321-329
2. Update the UI info window (UIFractionatorWindow__OnUpdate_Postfix) to show the correct formula