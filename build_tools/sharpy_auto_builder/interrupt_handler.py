"""
CLI Interrupt Handler for Sharpy Auto Builder.

Handles display and collection of responses for LangGraph interrupts.
Uses rich for formatted terminal output.
"""

from typing import Dict, List, Optional, Any
from rich.console import Console
from rich.panel import Panel
from rich.table import Table
from rich.prompt import Prompt, Confirm
from rich.markdown import Markdown
from rich.syntax import Syntax
from rich import box

console = Console()


def display_interrupt(interrupt_data: Dict[str, Any]) -> None:
    """
    Display an interrupt request to the user with formatted output.

    Args:
        interrupt_data: The interrupt payload dictionary containing type and details
    """
    interrupt_type = interrupt_data.get("type", "unknown")

    if interrupt_type == "review":
        _display_review_request(interrupt_data)
    elif interrupt_type == "question":
        _display_question(interrupt_data)
    else:
        console.print(f"[yellow]Unknown interrupt type: {interrupt_type}[/yellow]")
        console.print(interrupt_data)


def _display_review_request(data: Dict[str, Any]) -> None:
    """
    Display a review request with execution results and validation details.

    Args:
        data: Review payload with task info, execution results, and validations
    """
    task_id = data.get("task_id", "unknown")
    task_desc = data.get("task_description", "")
    execution_result = data.get("execution_result", {})
    validation_results = data.get("validation_results", [])
    files_changed = data.get("files_changed", [])
    diff_summary = data.get("diff_summary", "")
    diff_content = data.get("diff_content", "")  # Full diff for code review
    validation_error = data.get("validation_error")
    attempt = data.get("attempt", 0)

    # Title panel
    title = f"[bold cyan]Human Review Required[/bold cyan]"
    if attempt > 0:
        title += f" [yellow](Attempt {attempt + 1})[/yellow]"

    console.print(Panel(title, box=box.DOUBLE))
    console.print()

    # Show validation error if this is a retry
    if validation_error:
        console.print(
            Panel(
                f"[red bold]Previous response was invalid:[/red bold]\n{validation_error}",
                title="Validation Error",
                border_style="red",
            )
        )
        console.print()

    # Task information
    console.print(f"[bold]Task ID:[/bold] {task_id}")
    console.print(f"[bold]Description:[/bold] {task_desc}")
    console.print()

    # Execution result
    success = execution_result.get("success", False)
    status_color = "green" if success else "red"
    console.print(
        Panel(
            f"[{status_color}]{'✓ Success' if success else '✗ Failed'}[/{status_color}]",
            title="Execution Status",
        )
    )

    if execution_result.get("error"):
        console.print(f"[red]Error:[/red] {execution_result['error'][:200]}")
    console.print()

    # Files changed
    if files_changed:
        console.print(f"[bold]Files Changed ({len(files_changed)}):[/bold]")
        for file in files_changed[:10]:  # Show first 10
            console.print(f"  • {file}")
        if len(files_changed) > 10:
            console.print(f"  ... and {len(files_changed) - 10} more")
        console.print()

    # Diff summary (file statistics)
    if diff_summary:
        console.print("[bold]Changes Summary:[/bold]")
        console.print(Panel(diff_summary[:500], border_style="dim"))
        console.print()

    # Validation results
    if validation_results:
        table = Table(title="Validation Results", box=box.ROUNDED)
        table.add_column("Agent", style="cyan")
        table.add_column("Status", style="bold")
        table.add_column("Message", style="dim")

        for vr in validation_results:
            status = vr.get("status", "unknown")
            status_color = (
                "green"
                if status == "passed"
                else "red" if status == "failed" else "yellow"
            )
            table.add_row(
                vr.get("agent", "unknown"),
                f"[{status_color}]{status}[/{status_color}]",
                str(vr.get("message", ""))[:60],
            )

        console.print(table)
        console.print()

    # Full diff content (actual code changes for review)
    if diff_content:
        console.print("[bold cyan]═══ Code Changes (git diff) ═══[/bold cyan]")
        console.print()
        # Use Syntax for diff highlighting
        console.print(Syntax(diff_content, "diff", theme="monokai", line_numbers=False))
        console.print()
        console.print("[bold cyan]═══ End of Changes ═══[/bold cyan]")
        console.print()
    elif files_changed:
        # If we have files but no diff content, suggest running git diff manually
        console.print("[yellow]Note: Run 'git diff' to see full code changes[/yellow]")
        console.print()


def _display_question(data: Dict[str, Any]) -> None:
    """
    Display a question with context and options.

    Args:
        data: Question payload with question text, context, and options
    """
    task_id = data.get("task_id", "unknown")
    task_desc = data.get("task_description", "")
    question = data.get("question", "")
    context = data.get("context", "")
    priority = data.get("priority", "medium")
    options = data.get("options")
    validation_error = data.get("validation_error")
    attempt = data.get("attempt", 0)

    # Title with priority indicator
    priority_color = {"high": "red", "medium": "yellow", "low": "blue"}.get(
        priority, "white"
    )
    title = f"[bold {priority_color}]Question ({priority.upper()} priority)[/bold {priority_color}]"
    if attempt > 0:
        title += f" [yellow](Attempt {attempt + 1})[/yellow]"

    console.print(Panel(title, box=box.DOUBLE))
    console.print()

    # Show validation error if this is a retry
    if validation_error:
        console.print(
            Panel(
                f"[red bold]Previous answer was invalid:[/red bold]\n{validation_error}",
                title="Validation Error",
                border_style="red",
            )
        )
        console.print()

    # Task info
    console.print(f"[bold]Task ID:[/bold] {task_id}")
    if task_desc:
        console.print(f"[bold]Task:[/bold] {task_desc}")
    console.print()

    # Question
    console.print(Panel(question, title="Question", border_style="cyan"))
    console.print()

    # Context if provided
    if context:
        console.print("[bold]Context:[/bold]")
        console.print(Panel(context[:500], border_style="dim"))
        console.print()

    # Options if provided
    if options:
        console.print("[bold]Available Options:[/bold]")
        for i, option in enumerate(options, 1):
            console.print(f"  {i}. {option}")
        console.print()


def collect_response(interrupt_data: Dict[str, Any]) -> Dict[str, Any]:
    """
    Collect user response for an interrupt.

    Args:
        interrupt_data: The interrupt payload

    Returns:
        Dictionary containing the user's response
    """
    interrupt_type = interrupt_data.get("type", "unknown")

    if interrupt_type == "review":
        return _collect_review_response()
    elif interrupt_type == "question":
        return _collect_question_response(interrupt_data)
    else:
        console.print(f"[red]Unknown interrupt type: {interrupt_type}[/red]")
        return {"error": f"Unknown interrupt type: {interrupt_type}"}


def _collect_review_response() -> Dict[str, Any]:
    """
    Collect user's review decision.

    Returns:
        Dictionary with approved, retry, feedback fields
    """
    console.print("[bold yellow]What would you like to do?[/bold yellow]")
    console.print("  1. Approve - Accept the implementation and commit")
    console.print("  2. Retry - Request changes and re-run implementation")
    console.print("  3. Skip - Skip this task and move on")
    console.print()

    choice = Prompt.ask(
        "Enter your choice",
        choices=["1", "2", "3", "approve", "retry", "skip"],
        default="1",
    )

    # Normalize choice
    if choice in ["1", "approve"]:
        approved = True
        retry = False
        action = "approved"
    elif choice in ["2", "retry"]:
        approved = False
        retry = True
        action = "retry requested"
    else:  # 3 or skip
        approved = False
        retry = False
        action = "skipped"

    # Collect optional feedback
    console.print()
    has_feedback = Confirm.ask("Would you like to add feedback?", default=False)

    feedback = None
    if has_feedback:
        console.print("[dim]Enter feedback (press Enter twice to finish):[/dim]")
        lines = []
        while True:
            line = input()
            if not line and lines and not lines[-1]:
                break
            lines.append(line)
        feedback = "\n".join(lines).strip() or None

    console.print()
    console.print(f"[green]✓ Review {action}[/green]")
    if feedback:
        console.print(f"[dim]Feedback: {feedback[:100]}...[/dim]")
    console.print()

    return {
        "approved": approved,
        "retry": retry,
        "feedback": feedback,
        "modified_value": None,
    }


def _collect_question_response(data: Dict[str, Any]) -> Dict[str, Any]:
    """
    Collect user's answer to a question.

    Args:
        data: Question payload with options

    Returns:
        Dictionary with value and optional additional_feedback
    """
    options = data.get("options")

    if options:
        # Multiple choice question
        console.print("[bold yellow]Please select an option:[/bold yellow]")
        for i, option in enumerate(options, 1):
            console.print(f"  {i}. {option}")
        console.print()

        # Build choices list with numbers and option strings
        choices = [str(i) for i in range(1, len(options) + 1)] + options

        choice = Prompt.ask(
            "Enter your choice (number or option text)", choices=choices
        )

        # Convert number to option text
        if choice.isdigit():
            idx = int(choice) - 1
            if 0 <= idx < len(options):
                answer = options[idx]
            else:
                answer = choice
        else:
            answer = choice
    else:
        # Free text question
        console.print("[bold yellow]Please enter your answer:[/bold yellow]")
        answer = Prompt.ask("Answer")

    # Collect optional additional feedback
    console.print()
    has_feedback = Confirm.ask(
        "Would you like to add additional feedback?", default=False
    )

    additional_feedback = None
    if has_feedback:
        console.print("[dim]Enter additional feedback:[/dim]")
        additional_feedback = Prompt.ask("Feedback")

    console.print()
    console.print(f"[green]✓ Answer recorded: {answer}[/green]")
    if additional_feedback:
        console.print(f"[dim]Additional feedback: {additional_feedback}[/dim]")
    console.print()

    return {"value": answer, "additional_feedback": additional_feedback}
