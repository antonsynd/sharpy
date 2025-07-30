/*
Sharpy grammar
The MIT License (MIT)
Copyright (c) 2025 Anton Nguyen

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
 */

// A substantial portion of this grammar is from
// the Python grammar at https://github.com/RobEin/ANTLR4-parser-for-Python-3.13

/*
Python grammar
The MIT License (MIT)
Copyright (c) 2021 Robert Einhorn

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
 */

 /*
  * Project      : an ANTLR4 parser grammar by the official PEG grammar
  *                https://github.com/RobEin/ANTLR4-parser-for-Python-3.13
  * Developed by : Robert Einhorn
  *
  */

 /*
  * Contributors : [Willie Shen](https://github.com/Willie169)
  */

// Python 3.13.2  https://docs.python.org/3.13/reference/grammar.html#full-grammar-specification

parser grammar SharpyParser;

options { tokenVocab=SharpyLexer; }

// STARTING RULES
// ==============

// A module simply contains a series of statements.
module: statement+ EOF;

// Compound statements are statements that can contain other statements, e.g.
// function definitions, for-blocks, etc.
// Simple statements are statements that cannot contain other statements, e.g.
// assignments, return statements, etc.
statement: compound_statement | simple_statement NEWLINE;

// NOTE: assignment MUST precede expression, else parsing a simple assignment
// will throw a SyntaxError.
simple_statement
    : assignment
    | type_alias
    | return_statement
    | import_statement
    | raise_statement
    | pass_statement
    | del_statement
    | yield_statement
    | assert_statement
    | break_statement
    | continue_statement
    | global_statement
    | nonlocal_statement
    | expression_statement;

expression_statement
    : expressions;

compound_statement
    : function_def
    | if_statement
    | class_def
    | protocol_def
    | struct_def
    | with_statement
    | for_statement
    | try_statement
    | while_statement
    | match_statement;

// SIMPLE STATEMENTS
// =================

// NOTE: annotated_rhs may start with 'yield'; yield_expression must start with 'yield'
assignment
    : target=typed_name (EQUAL annotated_rhs )?
    | (LPAR single_target RPAR
         | single_subscript_attribute_target) COLON expression (EQUAL annotated_rhs )?
    | (star_targets EQUAL )+ yield_expression
    | single_target augmented_assignment yield_expression;

annotated_rhs: yield_expression;

augmented_assignment
    : PLUSEQUAL
    | MINEQUAL
    | STAREQUAL
    | ATEQUAL
    | SLASHEQUAL
    | PERCENTEQUAL
    | AMPEREQUAL
    | VBAREQUAL
    | CIRCUMFLEXEQUAL
    | LEFTSHIFTEQUAL
    | RIGHTSHIFTEQUAL
    | DOUBLESTAREQUAL
    | DOUBLESLASHEQUAL;

pass_statement: PASS;

break_statement: BREAK;
continue_statement: CONTINUE;

return_statement
    : RETURN expressions?;

raise_statement
    : RAISE (error=expression (FROM parent=expression )?)?
    ;

global_statement: GLOBAL name (COMMA name)*;

nonlocal_statement: NONLOCAL name (COMMA name)*;

del_statement
    : DEL del_targets;

yield_statement: yield_expression;

assert_statement: ASSERT expression (COMMA expression )?;

import_statement
    : import_name
    | import_from;

// Import statements
// -----------------

import_name: IMPORT dotted_as_names;
// note below: the ('.' | '...') is necessary because '...' is tokenized as ELLIPSIS
import_from
    : FROM ('.' | '...')* dotted_name IMPORT import_from_targets
    | FROM ('.' | '...')+ IMPORT import_from_targets;
import_from_targets
    : LPAR import_from_as_names COMMA? RPAR
    | import_from_as_names
    | STAR;
import_from_as_names
    : import_from_as_name (COMMA import_from_as_name)*;
import_from_as_name
    : name (AS name )?;
dotted_as_names
    : dotted_as_name (COMMA dotted_as_name)*;
dotted_as_name
    : dotted_name (AS name )?;
dotted_name
    : dotted_name member_access name
    | name;

// COMPOUND STATEMENTS
// ===================

// Common elements
// ---------------

block: NEWLINE INDENT statement+ DEDENT;

decorators: (AT named_expression NEWLINE )+;

// Class/struct/protocol definitions
// -----------------

class_def
    : decorators? CLASS name type_params? (LPAR arguments? RPAR )? COLON block;

struct_def
    : decorators? STRUCT name type_params? (LPAR arguments? RPAR )? COLON block;

protocol_def
    : decorators? PROTOCOL name type_params? (LPAR arguments? RPAR )? COLON block;

// Function definitions
// --------------------

function_def
    : decorators? DEF name type_params? LPAR params? RPAR (RARROW expression )? COLON block
    ;

async_function_def
    : decorators? ASYNC DEF name type_params? LPAR params? RPAR (RARROW expression )? COLON block
    ;

// Property definitions
// --------------------

property_def
    : PROPERTY name COLON block
    | PROPERTY name COLON get_block set_block?
    | PROPERTY name COLON set_block get_block?
    ;

get_block: 'get' LPAR name RPAR COLON block;

set_block: 'set' LPAR name COMMA typed_name RPAR COLON block;

// Event definitions
// -----------------

event_def: 'event' typed_name;

// Function parameters
// -------------------

params
    : parameters;

parameters
    : param_no_default+ param_with_default*
    | param_with_default+
    ;

param_no_default
    : param COMMA?
    ;

param_with_default
    : param default_assignment COMMA?
    ;

param: name type_annotation?;
type_annotation: COLON expression;
default_assignment: EQUAL expression;

// If statement
// ------------

if_statement
    : IF named_expression COLON block else_blocks
    ;

else_blocks
    : (elif_statement)* else_block?
    ;

elif_statement
    : ELIF named_expression COLON block
    ;

else_block
    : ELSE COLON block;

// While statement
// ---------------

while_statement
    : WHILE named_expression COLON block else_block?;

// For statement
// -------------

for_statement
    : ASYNC? FOR star_targets IN expressions COLON block else_block?
    ;

// With statement
// --------------

with_statement
    : WITH LPAR with_item (COMMA with_item)* COMMA? RPAR COLON block
    | ASYNC WITH LPAR with_item (COMMA with_item)* COMMA? RPAR COLON block
    | ASYNC? WITH with_item (COMMA with_item)* COLON block
    ;

with_item
    : expression (AS star_target)?
    ;

// Try statement
// -------------

try_statement
    : TRY COLON block finally_block
    | TRY COLON block except_block+ else_block? finally_block?
    | TRY COLON block except_star_block+ else_block? finally_block?;

// Except statement
// ----------------

except_block
    : EXCEPT (expression (AS name )?)? COLON block
    ;
except_star_block
    : EXCEPT STAR expression (AS name )? COLON block;
finally_block
    : FINALLY COLON block;

// Match statement
// ---------------

match_statement
    : 'match' subject_expression COLON NEWLINE INDENT case_block+ DEDENT;

subject_expression: named_expression;

case_block
    : 'case' patterns guard? COLON block;

guard: IF named_expression;

patterns
    : open_sequence_pattern
    | pattern;

pattern
    : as_pattern
    | or_pattern;

as_pattern
    : or_pattern AS pattern_capture_target;

or_pattern
    : closed_pattern (VBAR closed_pattern)*;

closed_pattern
    : literal_pattern
    | capture_pattern
    | wildcard_pattern
    | value_pattern
    | group_pattern
    | sequence_pattern
    | mapping_pattern
    | class_pattern;

// Literal patterns are used for equality and identity constraints
literal_pattern
    : signed_number
    | complex_number
    | strings
    | none_literal
    | true_literal
    | false_literal;

none_literal: NONE;
true_literal: TRUE;
false_literal: FALSE;

// Literal expressions are used to restrict permitted mapping pattern keys
literal_expression
    : signed_number
    | complex_number
    | strings
    | none_literal
    | true_literal
    | false_literal;

complex_number
    : signed_real_number (PLUS | MINUS) imaginary_number
    ;

signed_number
    : MINUS? NUMBER
    ;

signed_real_number
    : MINUS? real_number
    ;

real_number
    : NUMBER;

imaginary_number
    : NUMBER;

capture_pattern
    : pattern_capture_target;

pattern_capture_target
    : name_except_underscore;

wildcard_pattern
    : '_';

value_pattern
    : attr;

attr
    : name (member_access name)+
    ;
name_or_attr
    : name (member_access name)*
    ;

group_pattern
    : LPAR pattern RPAR;

sequence_pattern
    : LSQB maybe_sequence_pattern? RSQB
    | LPAR open_sequence_pattern? RPAR;

open_sequence_pattern
    : maybe_star_pattern COMMA maybe_sequence_pattern?;

maybe_sequence_pattern
    : maybe_star_pattern (COMMA maybe_star_pattern)* COMMA?;

maybe_star_pattern
    : star_pattern
    | pattern;

star_pattern
    : STAR name
    ;

mapping_pattern
    : LBRACE RBRACE
    | LBRACE double_star_pattern COMMA? RBRACE
    | LBRACE items_pattern (COMMA double_star_pattern)? COMMA? RBRACE
    ;

items_pattern
    : key_value_pattern (COMMA key_value_pattern)*;

key_value_pattern
    : (literal_expression | attr) COLON pattern;

double_star_pattern
    : DOUBLESTAR pattern_capture_target;

class_pattern
    : name_or_attr LPAR ((positional_patterns (COMMA keyword_patterns)? | keyword_patterns) COMMA?)? RPAR
    ;

positional_patterns
    : pattern (COMMA pattern)*;

keyword_patterns
    : keyword_pattern (COMMA keyword_pattern)*;

keyword_pattern
    : name EQUAL pattern;

// Type statement
// ---------------

type_alias
    : 'type' name type_params? EQUAL expression;

// Type parameter declaration
// --------------------------

type_params: LSQB type_param_seq  RSQB;

type_param_seq: type_param (COMMA type_param)* COMMA?;

type_param
    : name type_param_bound? type_param_default?
    ;

type_param_bound: COLON expression;
type_param_default: EQUAL expression;

// EXPRESSIONS
// -----------

expressions
    : expression (COMMA expression )* COMMA?
    ;

expression
    : (disjunction | ternary_expression)
    | lambda_def
    ;

// ast.IfExp()
ternary_expression
    : body=disjunction IF test=disjunction ELSE orelse=expression
    ;

yield_expression
    : YIELD FROM expression
    ;

// Sharpy allows for match expressions similar to C# switch expressions
// TODO: Should be in an assignment
match_expression
    : NAME_OR_MATCH subject_expression COLON NEWLINE INDENT case_block+ DEDENT
    ;

// Sharpy borrows try expressions from Swift
// TODO: Should be in an assignment, and maybe not restricted to function calls
try_expression
    : TRY await_primary
    ;

assignment_expression
    : name COLONEQUAL expression;

named_expressions: named_expression (COMMA named_expression)* COMMA?;

named_expression
    : assignment_expression
    | expression;

disjunction
    : conjunction (OR conjunction )*
    ;

conjunction
    : inversion (AND inversion )*
    ;

inversion
    : NOT inversion
    | comparison;

// Comparison operators
// --------------------

comparison
    : bitwise_or compare_op_bitwise_or_pair*
    ;

compare_op_bitwise_or_pair
    : eq_bitwise_or
    | noteq_bitwise_or
    | lte_bitwise_or
    | lt_bitwise_or
    | gte_bitwise_or
    | gt_bitwise_or
    | notin_bitwise_or
    | in_bitwise_or
    | isnot_bitwise_or
    | is_bitwise_or;

eq_bitwise_or: EQEQUAL bitwise_or;
noteq_bitwise_or
    : NOTEQUAL bitwise_or;
lte_bitwise_or: LESSEQUAL bitwise_or;
lt_bitwise_or: LESS bitwise_or;
gte_bitwise_or: GREATEREQUAL bitwise_or;
gt_bitwise_or: GREATER bitwise_or;
notin_bitwise_or: NOT IN bitwise_or;
in_bitwise_or: IN bitwise_or;
isnot_bitwise_or: IS NOT bitwise_or;
is_bitwise_or: IS bitwise_or;

// Bitwise operators
// -----------------

bitwise_or
    : bitwise_or VBAR bitwise_xor
    | bitwise_xor;

bitwise_xor
    : bitwise_xor CIRCUMFLEX bitwise_and
    | bitwise_and;

bitwise_and
    : bitwise_and AMPER shift_expression
    | shift_expression;

shift_expression
    : shift_expression (leftshift_operator | rightshift_operator) sum
    | sum
    ;

leftshift_operator: LEFTSHIFT;
rightshift_operator: RIGHTSHIFT;

// Arithmetic operators
// --------------------

sum
    : sum (addition_operator | subtraction_operator) term
    | term
    ;

addition_operator: PLUS;
subtraction_operator: MINUS;

term
    : term (multiplication_operator | division_operator | floor_division_operator | modulo_operator | matrix_multiplication_operator) factor
    | factor
    ;

multiplication_operator: STAR;
division_operator: SLASH;
floor_division_operator: DOUBLESLASH;
modulo_operator: PERCENT;
matrix_multiplication_operator: AT;

factor
    : positive_sign factor
    | negative_sign factor
    | inversion_operator factor
    | power;

positive_sign
    : PLUS;
negative_sign
    : MINUS;
inversion_operator
    : TILDE;

power
    : await_primary (DOUBLESTAR factor)?
    ;

// Primary elements
// ----------------

// Primary elements are things like "obj.something.something", "obj[something]", "obj(something)", "obj" ...

await_primary
    : AWAIT primary
    | primary;

primary
    : primary (member_access name | generator_expression | LPAR arguments? RPAR | LSQB slices RSQB)
    | atom
    ;

slices
    : slice
    | slice (COMMA slice)* COMMA?;

slice
    : expression? COLON expression? (COLON expression? )?
    | named_expression;

ellipsis_literal: ELLIPSIS;

atom
    : name
    | none_literal
    | true_literal
    | false_literal
    | strings
    | NUMBER
    | (tuple | group | generator_expression)
    | (list | list_comprehension)
    | (dict | set | dict_comprehension | set_comprehension)
    | ellipsis_literal;

group
    : LPAR (yield_expression | named_expression) RPAR;

// Lambda functions
// ----------------

// Sharpy lambdas allow return type annotations.
lambda_def
    : LAMBDA params? ( RARROW expression )? COLON expression;

// LITERALS
// ========

fstring_middle
    : fstring_replacement_field
    | FSTRING_MIDDLE;
fstring_replacement_field
    : LBRACE annotated_rhs EQUAL? fstring_conversion? fstring_full_format_spec? RBRACE;
fstring_conversion
    : EXCLAMATION name;
fstring_full_format_spec
    : COLON fstring_format_spec*;
fstring_format_spec
    : FSTRING_MIDDLE
    | fstring_replacement_field;
fstring
    : FSTRING_START fstring_middle* FSTRING_END;

string: STRING;
strings: (fstring|string)+;

list
    : LSQB named_expressions? RSQB;

tuple
    : LPAR (named_expression COMMA named_expressions? )? RPAR;

set: LBRACE named_expressions RBRACE;

// Dicts
// -----

dict
    : LBRACE kvpairs? RBRACE;

kvpairs
    : kvpair (COMMA kvpair)* COMMA?;

// TODO: This doesn't work for some reason, I used direct references to the
// children instead.
kvpair: key_expression COLON value_expression;

key_expression: expression;
value_expression: expression;

// Comprehensions & Generators
// ---------------------------

for_if_clauses
    : for_if_clause+;

for_if_clause
    : ASYNC? FOR star_targets IN disjunction (IF disjunction )*
    ;

list_comprehension
    : LSQB named_expression for_if_clauses RSQB;

set_comprehension
    : LBRACE named_expression for_if_clauses RBRACE;

generator_expression
    : LPAR ( assignment_expression | expression) for_if_clauses RPAR;

dict_comprehension
    : LBRACE kvpair for_if_clauses RBRACE;

// FUNCTION CALL ARGUMENTS
// =======================

arguments
    : args COMMA?;

args
    : ( assignment_expression | expression) (COMMA ( assignment_expression | expression))* (COMMA kwargs )?
    | kwargs;

kwargs: kwarg (COMMA kwarg)* COMMA?;

kwarg: name EQUAL expression;

// ASSIGNMENT TARGETS
// ==================

// Generic targets
// ---------------

// NOTE: star_targets may contain *bitwise_or, targets may not.
star_targets
    : star_target (COMMA star_target )* COMMA?
    ;

star_targets_list_seq: star_target (COMMA star_target)* COMMA?;

star_targets_tuple_seq
    : star_target (COMMA | (COMMA star_target )+ COMMA?)
    ;

star_target
    : STAR (star_target)
    | target_with_star_atom;

target_with_star_atom
    : t_primary (member_access name | LSQB slices RSQB)
    | star_atom
    ;

star_atom
    : name
    | LPAR target_with_star_atom RPAR
    | LPAR star_targets_tuple_seq? RPAR
    | LSQB star_targets_list_seq? RSQB;

single_target
    : single_subscript_attribute_target
    | name
    | LPAR single_target RPAR;

single_subscript_attribute_target
    : t_primary (member_access name | LSQB slices RSQB)
    ;

t_primary
    : t_primary (member_access name | LSQB slices RSQB | generator_expression | LPAR arguments? RPAR)
    | atom
    ;

// Targets for del statements
// --------------------------

// Allows trailing comma
del_targets: del_target (COMMA del_target)* COMMA?;

// Sharpy only allows del statements with dictionary keys, not names
del_target: t_primary (member_access name | LSQB slices RSQB);

// TYPING ELEMENTS
// ---------------

member_access: DOT | QUESTIONDOT;

// *** related to soft keywords: https://docs.python.org/3.13/reference/lexical_analysis.html#soft-keywords
// This is used in match case blocks because _ means wildcard there, not a name
name_except_underscore
    : NAME // ***** The NAME token can be used only in this rule *****
    | NAME_OR_TYPE
    | NAME_OR_MATCH
    | NAME_OR_CASE
    | NAME_OR_EVENT
    | NAME_OR_GET
    | NAME_OR_SET
    ;

// ***** Always use name rule instead of NAME token in this grammar *****
name: NAME_OR_WILDCARD | name_except_underscore;

typed_name: name type_annotation;

// ========================= END OF THE GRAMMAR ===========================
