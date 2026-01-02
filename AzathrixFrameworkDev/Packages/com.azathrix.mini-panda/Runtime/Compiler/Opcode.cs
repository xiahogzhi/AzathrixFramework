namespace Azathrix.MiniPanda.Compiler
{
    public enum Opcode : byte
    {
        // Stack operations
        Pop,
        Dup,

        // Constants
        Const,          // Push constant from pool
        Null,
        True,
        False,

        // Variables
        GetLocal,       // Get local variable by slot
        SetLocal,       // Set local variable by slot
        GetGlobal,      // Get global variable by name index
        SetGlobal,      // Set global variable by name index
        DefineGlobal,   // Define global variable

        // Arithmetic
        Add,
        Sub,
        Mul,
        Div,
        Mod,
        Neg,            // Unary minus

        // Comparison
        Eq,
        Ne,
        Lt,
        Le,
        Gt,
        Ge,

        // Logical
        Not,
        And,
        Or,

        // Control flow
        Jump,           // Unconditional jump
        JumpIfFalse,    // Jump if top of stack is false
        JumpIfTrue,     // Jump if top of stack is true
        Loop,           // Jump backward

        // Functions
        Call,           // Call function with N arguments
        Return,         // Return from function
        Closure,        // Create closure

        // Objects and arrays
        NewArray,       // Create array with N elements
        NewObject,      // Create empty object
        GetField,       // Get object field by name index
        SetField,       // Set object field by name index
        GetIndex,       // Get array/object element by index
        SetIndex,       // Set array/object element by index

        // Classes
        Class,          // Define class
        Inherit,        // Set up inheritance
        Method,         // Define method
        GetProperty,    // Get property (with method binding)
        SetProperty,    // Set property
        GetSuper,       // Get super method
        Invoke,         // Optimized method call
        SuperInvoke,    // Optimized super method call

        // Special
        Import,         // Import module
        This,           // Push this
        BuildString,    // Build interpolated string

        // Iterator
        GetIter,        // Get iterator from iterable
        ForIter,        // Advance iterator, jump if done

        // Upvalues
        GetUpvalue,     // Get captured variable
        SetUpvalue,     // Set captured variable
        CloseUpvalue,   // Close captured variable

        // Stack helpers
        Dup2,           // Duplicate top two stack values
        SwapUnder,      // Swap the two values under the top
        Rot3Under,      // Rotate the three values under the top
        Swap,           // Swap top two values

        // Root globals (for global keyword)
        DefineRootGlobal, // Define variable in root globals
    }
}
