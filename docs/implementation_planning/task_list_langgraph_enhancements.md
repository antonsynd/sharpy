# LangGraph Orchestrator Enhancements Task List

> **Purpose**: This consolidated task list combines Implementation Plans 1-4 and their amendments into a format compatible with the Sharpy Auto Builder. Tasks implement durable persistence, native interrupts, idempotent tasks, and long-term memory for the orchestrator.

> **Source**: Consolidated from `implementation_plan_1_durable_persistence.md`, `implementation_plan_2_native_interrupts.md`, `implementation_plan_3_idempotent_tasks.md`, `implementation_plan_4_memory_store.md`, and `implementation_plan_amendments.md`.

> **Priority Order**: Phase 1 (High) → Phase 2 (High, depends on 1) → Phase 3 (High, depends on 1) → Phase 4 (Medium, independent)

---

## Legend

| Symbol | Meaning |
|--------|---------|
| 🔧 | Implementation required |
| 📁 | File location |
| ⚠️ | Critical consideration |
| ✅ | Verification step |
| 🚨 | High priority |
| 🟠 | Medium priority |
| 🟢 | Low priority |

---

## Phase 1.0.0: Durable Persistence

**Goal:** Replace in-memory `MemorySaver` checkpointer with `SqliteSaver` to enable true durable execution that survives process restarts, rate limiting pauses, and crashes.

**Priority:** 🚨 High
**Estimated Effort:** 2-4 hours
**Risk Level:** Low (mostly configuration changes)

---

### Task 1.0.0.1: Add SqliteSaver Dependency

🔧 **Priority**: 🚨 High

📁 **Files**: `build_tools/sharpy_auto_builder/requirements.txt`

**Actions**:

1. [x] Add `langgraph-checkpoint-sqlite>=2.0.0` to requirements.txt

**Verification**:
- ✅ Run: `pip install -r requirements.txt`
- ✅ Run: `python -c "from langgraph.checkpoint.sqlite import SqliteSaver; print('OK')"`

---

### Task 1.0.0.2: Add Checkpoint Path to Configuration

🔧 **Priority**: 🚨 High

📁 **Files**: `build_tools/sharpy_auto_builder/config.py`

**Actions**:

1. [x] Add `checkpoint_db_path` property to `Config` class returning `self.state_dir / "orchestrator_checkpoints.db"`
2. [x] Add `CheckpointConfig` dataclass with fields:
   - `durability_mode: Literal["async", "sync"] = "async"`
   - `max_checkpoints_per_thread: int = 100`
   - `cleanup_interval: int = 50`
   - `retain_failed_checkpoints_days: int = 7`
3. [x] Add `checkpoint: CheckpointConfig` field to main `Config` class

**Verification**:
- ✅ Test: `Config().checkpoint_db_path` returns valid path
- ✅ Test: `Config().checkpoint.max_checkpoints_per_thread` returns 100

---

### Task 1.0.0.3: Update Orchestrator to Use SqliteSaver

🔧 **Priority**: 🚨 High

📁 **Files**: `build_tools/sharpy_auto_builder/orchestrator.py`

**Actions**:

1. [x] Remove import: `from langgraph.checkpoint.memory import MemorySaver`
2. [x] Add imports: `import sqlite3` and `from langgraph.checkpoint.sqlite import SqliteSaver`
3. [x] In `__init__`, replace `self.memory = MemorySaver()` with:
   - Create `self._db_connection = sqlite3.connect(str(self.config.checkpoint_db_path), check_same_thread=False)`
   - Create `self.checkpointer = SqliteSaver(self._db_connection)`
   - Call `self.checkpointer.setup()`
4. [x] Update `self.app = self.graph.compile(checkpointer=self.checkpointer)`
5. [x] Add `close()` method to close database connection
6. [x] Add `__del__` method calling `self.close()`
7. [x] Add `__enter__` and `__exit__` for context manager support

**Verification**:
- ✅ Test: `orchestrator_checkpoints.db` created in `state/` directory
- ✅ Test: Context manager properly closes connection

---

### Task 1.0.0.4: Add Checkpoint Retention and Cleanup

🔧 **Priority**: 🟠 Medium (Amendment 1.A)

📁 **Files**: `build_tools/sharpy_auto_builder/orchestrator.py`

**Actions**:

1. [x] Add `_setup_checkpoint_cleanup()` method initializing `_checkpoint_count` and `_cleanup_interval`
2. [x] Add `_maybe_cleanup_checkpoints(thread_id)` method that runs cleanup periodically
3. [x] Add `_cleanup_thread_checkpoints(thread_id)` method to remove old checkpoints beyond `max_checkpoints_per_thread`
4. [x] Add `get_checkpoint_stats()` method returning checkpoint counts, thread info, and database size
5. [x] Call `_setup_checkpoint_cleanup()` in `__init__`

**Verification**:
- ✅ Test: Cleanup runs every N checkpoints
- ✅ Test: `get_checkpoint_stats()` returns accurate counts

---

### Task 1.0.0.5: Add Thread ID Management

🔧 **Priority**: 🚨 High

📁 **Files**: `build_tools/sharpy_auto_builder/orchestrator.py`

**Actions**:

1. [x] Update `run()` method signature to accept `thread_id: Optional[str] = None`
2. [x] Generate thread ID if not provided: `f"sharpy-build-{datetime.now().strftime('%Y%m%d-%H%M%S')}"`
3. [x] Store as `self._current_thread_id`
4. [x] Print thread ID with resume instructions
5. [x] Check for existing state via `self.app.get_state(config)` to detect resume vs new session
6. [x] Add `_create_initial_state()` helper method

**Verification**:
- ✅ Test: Thread ID printed on run start
- ✅ Test: Resume instructions shown

---

### Task 1.0.0.6: Update CLI to Support Resume

🔧 **Priority**: 🚨 High

📁 **Files**: `build_tools/sharpy_auto_builder/cli.py`

**Actions**:

1. [x] Add `--thread-id` argument to resume previous sessions
2. [x] Add `--list-sessions` argument to show saved sessions
3. [x] Add `list_sessions(config)` function querying checkpoint database for unique thread IDs
4. [x] Add `checkpoint-stats` command showing storage statistics
5. [x] Add `checkpoint-cleanup` command with `--thread-id`, `--keep`, `--dry-run` options

**Verification**:
- ✅ Test: `./auto_builder.sh run --thread-id <id>` resumes session
- ✅ Test: `./auto_builder.sh checkpoint-stats` shows statistics

---

### Task 1.0.0.7: Add Recovery From Rate Limit

🔧 **Priority**: 🟠 Medium

📁 **Files**: `build_tools/sharpy_auto_builder/orchestrator.py`

**Actions**:

1. [x] Update `_handle_error_node` to detect rate limiting
2. [x] When rate limited, print session checkpoint message with thread ID
3. [x] Print resume command: `./auto_builder.sh run --thread-id {thread_id}`
4. [x] Return state with `next_action: "pause_rate_limited"`
5. [x] Add `pause_rate_limited` to graph routing (routes to END)

**Verification**:
- ✅ Test: Rate limit shows resume instructions
- ✅ Test: Session can be resumed after rate limit

---

### Task 1.0.0.8: Write Persistence Tests

🔧 **Priority**: 🟢 Low

📁 **Files**: `build_tools/tests/test_orchestrator_persistence.py`

**Actions**:

1. [x] Create test file with `TestCheckpointPersistence` class
2. [x] Test checkpoint database creation on init
3. [x] Test required tables are created
4. [x] Test session can be resumed with thread_id
5. [x] Add `TestThreadIdManagement` class
6. [x] Add `TestCleanup` class for resource cleanup tests
7. [x] Add `TestCheckpointStats` class for statistics testing
8. [x] Add `TestRateLimitRecovery` class for rate limit handling tests
9. [x] Add `TestGraphRouting` class for graph routing tests

**Verification**:
- ✅ Run: `pytest build_tools/tests/test_orchestrator_persistence.py`
- ✅ All 16 tests pass

---

## Phase 2.0.0: Native Interrupts for Human-in-the-Loop

**Goal:** Replace file-based polling system for human input with LangGraph's native `interrupt()` function for cleaner code and automatic state persistence.

**Priority:** 🚨 High
**Estimated Effort:** 4-6 hours
**Risk Level:** Medium (changes core human interaction flow)

⚠️ **Prerequisite:** Phase 1.0.0 (Durable Persistence) must be completed first.

---

### Task 2.0.0.1: Update Imports in Orchestrator

🔧 **Priority**: 🚨 High

📁 **Files**: `build_tools/sharpy_auto_builder/orchestrator.py`

**Actions**:

1. [x] Add import: `from langgraph.types import interrupt, Command`
2. [x] Keep existing import: `from langgraph.graph import StateGraph, END`

**Verification**:
- ✅ Test: Imports resolve without error

---

### Task 2.0.0.2: Create Interrupt Data Structures

🔧 **Priority**: 🚨 High

📁 **Files**: `build_tools/sharpy_auto_builder/orchestrator.py`

**Actions**:

1. [x] Add `HumanQuestionPayload` TypedDict with fields: `type`, `task_id`, `task_description`, `question`, `priority`, `context`, `options`
2. [x] Add `HumanReviewPayload` TypedDict with fields: `type`, `task_id`, `task_description`, `execution_result`, `validation_results`, `files_changed`, `diff_summary`
3. [x] Add `HumanResponse` TypedDict with fields: `approved`, `feedback`, `modified_value`, `retry`

**Verification**:
- ✅ Test: TypedDicts can be instantiated

---

### Task 2.0.0.3: Rewrite Human Review Node with Interrupt

🔧 **Priority**: 🚨 High

📁 **Files**: `build_tools/sharpy_auto_builder/orchestrator.py`

**Actions**:

1. [x] Rewrite `_request_human_review_node` to use `interrupt(review_payload)` instead of file-based polling
2. [x] Build `HumanReviewPayload` with task info, execution result, validation results, files changed
3. [x] Call `human_response: HumanResponse = interrupt(review_payload)`
4. [x] After interrupt resumes, log response and route based on `approved`, `retry`, or skip
5. [x] Ensure pre-interrupt code is idempotent (no side effects)

⚠️ **Critical**: Code before `interrupt()` will re-run on resume. Keep it minimal and idempotent.

**Verification**:
- ✅ Test: Interrupt pauses execution
- ✅ Test: Resume continues from interrupt point

---

### Task 2.0.0.4: Add Question-Asking Helper with Interrupt

🔧 **Priority**: 🟠 Medium

📁 **Files**: `build_tools/sharpy_auto_builder/orchestrator.py`

**Actions**:

1. [x] Add `_ask_human_question(task_id, question, priority, context, options)` method
2. [x] Build `HumanQuestionPayload` and call `interrupt(payload)`
3. [x] Return the human's response

**Verification**:
- ✅ Test: Questions interrupt and wait for response

---

### Task 2.0.0.5: Implement Input Validation Loop

🔧 **Priority**: 🟠 Medium (Amendment 2.B)

📁 **Files**: `build_tools/sharpy_auto_builder/orchestrator.py`

**Actions**:

1. [x] Add `interrupt_with_validation(payload, validator, max_attempts)` function
2. [x] Loop until valid response or max attempts reached
3. [x] On invalid input, update payload with `validation_error` and re-interrupt
4. [x] Add `validate_review_response(response)` function returning `(is_valid, error_message)`
5. [x] Add `validate_question_response(response, options)` function

**Verification**:
- ✅ Test: Invalid input triggers re-prompt
- ✅ Test: Valid input returns immediately

---

### Task 2.0.0.6: Update Graph Routing for Interrupt Handling

🔧 **Priority**: 🚨 High

📁 **Files**: `build_tools/sharpy_auto_builder/orchestrator.py`

**Actions**:

1. [x] Add `_route_after_human_response(state)` method routing based on `next_action`
2. [x] Return `"commit_changes"`, `"execute_implementation"`, `"update_ground_truth"`, or `"handle_error"`
3. [x] Update graph with conditional edge from `request_human_review`

**Verification**:
- ✅ Test: Routing works for approve, retry, skip

---

### Task 2.0.0.7: Remove wait_for_human Node

🔧 **Priority**: 🚨 High

📁 **Files**: `build_tools/sharpy_auto_builder/orchestrator.py`

**Actions**:

1. [x] Delete `_wait_for_human_node` method
2. [x] Remove `graph.add_node("wait_for_human", ...)` from `_build_graph`
3. [x] Remove edges involving `wait_for_human`
4. [ ] Optionally remove state fields: `awaiting_human_input`, `human_question_id`, `human_review_id` (kept for backwards compatibility)

**Verification**:
- ✅ Test: Graph builds without wait_for_human
- ✅ Test: Human interaction still works via interrupt

---

### Task 2.0.0.8: Create CLI Interrupt Handler

🔧 **Priority**: 🚨 High

📁 **Files**: `build_tools/sharpy_auto_builder/interrupt_handler.py`

**Actions**:

1. [x] Create new file `interrupt_handler.py`
2. [x] Add `display_interrupt(interrupt_data)` function using rich for pretty output
3. [x] Add `_display_review_request(data)` showing task, execution result, validations, files
4. [x] Add `_display_question(data)` showing question, context, options
5. [x] Add `collect_response(interrupt_data)` function for user input
6. [x] Add `_collect_review_response()` with approve/retry/skip choices
7. [x] Add `_collect_question_response(data)` handling options or free text

**Verification**:
- ✅ Test: Review requests display properly
- ✅ Test: Responses collected correctly

---

### Task 2.0.0.9: Update Main Run Loop for Interrupts

🔧 **Priority**: 🚨 High

📁 **Files**: `build_tools/sharpy_auto_builder/cli.py`

**Actions**:

1. [x] Create `run_with_interrupts(config, thread_id, max_tasks)` async function
2. [x] Loop: invoke graph, check for `__interrupt__` in result
3. [x] If interrupted: call `display_interrupt()`, `collect_response()`, resume with `Command(resume=response)`
4. [x] Check for `next_action == "complete"` or `"pause_rate_limited"` to exit
5. [x] Track tasks processed and respect `max_tasks` limit

**Verification**:
- ✅ Test: Interrupts handled interactively
- ✅ Test: Session completes or pauses correctly

---

### Task 2.0.0.10: Deprecate File-Based HumanLoopManager

🔧 **Priority**: 🟢 Low

📁 **Files**: `build_tools/sharpy_auto_builder/human_loop.py`

**Actions**:

1. [x] Add deprecation notice to module docstring
2. [x] Add `warnings.warn()` in `HumanLoopManager.__init__`
3. [x] Keep for backwards compatibility with batch processing

**Verification**:
- ✅ Test: Deprecation warning appears on import

---

### Task 2.0.0.11: Write Interrupt Tests

🔧 **Priority**: 🟢 Low

📁 **Files**: `build_tools/tests/test_orchestrator_interrupts.py`

**Actions**:

1. [x] Create test file with `TestInterruptPayloads` class
2. [x] Add `TestInterruptResume` class testing interrupt and Command(resume=) flow
3. [x] Add `TestInterruptHandler` class for CLI handler tests
4. [x] Add `TestInterruptValidation` class for validation loop tests

**Verification**:
- ✅ Run: `pytest build_tools/tests/test_orchestrator_interrupts.py` - 24 tests passed

---

## Phase 3.0.0: Idempotent Tasks for CLI Calls

**Goal:** Wrap CLI calls to Claude Code and Copilot in LangGraph `@task` decorators to ensure idempotent execution during graph replays and resumption.

**Priority:** 🚨 High
**Estimated Effort:** 3-5 hours
**Risk Level:** Medium (changes execution flow)

⚠️ **Prerequisite:** Phase 1.0.0 (Durable Persistence) should be completed first.

---

### Task 3.0.0.1: Create Tasks Module

🔧 **Priority**: 🚨 High

📁 **Files**: `build_tools/sharpy_auto_builder/tasks.py`

**Actions**:

1. [x] Create new file `tasks.py`
2. [x] Add `TaskExecutionResult` dataclass with fields: `success`, `output`, `error`, `backend`, `model`, `duration_seconds`, `exit_code`, `timestamp`, `input_hash`
3. [x] Add `to_dict()` and `from_dict()` methods
4. [x] Add `_compute_input_hash(*args, **kwargs)` helper function

**Verification**:
- ✅ Test: `TaskExecutionResult` serializes to JSON

---

### Task 3.0.0.2: Implement execute_claude_cli Task

🔧 **Priority**: 🚨 High

📁 **Files**: `build_tools/sharpy_auto_builder/tasks.py`

**Actions**:

1. [x] Import `from langgraph.func import task`
2. [x] Create `@task` decorated `execute_claude_cli(prompt, tools, model, timeout, working_dir, task_id, attempt)` async function
3. [x] Compute input hash for cache key
4. [x] Build command using claude CLI directly
5. [x] Execute via `asyncio.create_subprocess_exec`
6. [x] Handle timeout with `asyncio.wait_for`
7. [x] Check for rate limiting in output
8. [x] Return `TaskExecutionResult`

**Verification**:
- ✅ Test: Task executes Claude CLI
- ✅ Test: Same inputs return cached result on replay

---

### Task 3.0.0.3: Implement execute_copilot_cli Task

🔧 **Priority**: 🚨 High

📁 **Files**: `build_tools/sharpy_auto_builder/tasks.py`

**Actions**:

1. [x] Create `@task` decorated `execute_copilot_cli(prompt, tools, timeout, working_dir, task_id, attempt)` async function
2. [x] Similar implementation to `execute_claude_cli` but for Copilot
3. [x] Note: Copilot doesn't support model selection

**Verification**:
- ✅ Test: Task executes Copilot CLI

---

### Task 3.0.0.4: Implement run_tests Task

🔧 **Priority**: 🟠 Medium

📁 **Files**: `build_tools/sharpy_auto_builder/tasks.py`

**Actions**:

1. [x] Create `@task` decorated `run_tests(test_command, working_dir, timeout, task_id, attempt)` async function
2. [x] Split command with `shlex.split`
3. [x] Execute and capture output
4. [x] Return `TaskExecutionResult`

**Verification**:
- ✅ Test: Task runs test command
- ✅ Test: Results cached on replay

---

### Task 3.0.0.5: Add Fallback Idempotency Tracker

🔧 **Priority**: 🟠 Medium (Amendment 3.A)

📁 **Files**: `build_tools/sharpy_auto_builder/tasks.py`

**Actions**:

1. [x] Add `TaskIdempotencyFallback` class with file-based tracking
2. [x] Implement `_marker_path(input_hash)`, `get_cached(input_hash)`, `cache_result(input_hash, result)`
3. [x] Add global `_fallback_tracker` and `_get_fallback_tracker()` function
4. [x] Add `use_fallback_idempotency: bool = True` parameter to task functions (already present)
5. [x] Add usage documentation for orchestrator integration

⚠️ **Critical**: `@task` caching may not work in all deployment environments. Fallback provides reliability.

**Verification**:
- ✅ Test: Fallback cache works independently of @task

---

### Task 3.0.0.6: Update Orchestrator to Use Tasks

🔧 **Priority**: 🚨 High

📁 **Files**: `build_tools/sharpy_auto_builder/orchestrator.py`

**Actions**:

1. [x] Add import: `from .tasks import execute_claude_cli, execute_copilot_cli, run_tests, TaskExecutionResult, _get_fallback_tracker`
2. [x] Update `_execute_implementation_node` to call task functions with `task_id` and `attempt`
3. [x] Add `_execute_with_task_failover` helper method that:
   - Implements backend failover (Claude Code -> Copilot)
   - Integrates fallback idempotency tracker
   - Calls appropriate task function (execute_claude_cli or execute_copilot_cli)
   - Caches results in fallback tracker
4. [x] Convert result to dict for state storage (already in place)

**Verification**:
- ✅ Test: Execution uses task-wrapped functions
- ✅ Test: Imports successful
- ✅ Test: Method signature correct

---

### Task 3.0.0.7: Update Test Running Node

🔧 **Priority**: 🟠 Medium

📁 **Files**: `build_tools/sharpy_auto_builder/orchestrator.py`

**Actions**:

1. [x] Update `_run_baseline_tests_node` to use `run_tests` task with attempt=0
2. [x] Update `_run_tests_node` to use `run_tests` task with execution_attempt
3. [x] Pass `task_id` and `attempt` for cache differentiation
4. [x] Update timeout detection (exit_code == -1 and "timed out" in error)
5. [x] Log test results with exit_code instead of timed_out flag

**Verification**:
- ✅ Test: Baseline tests node calls run_tests
- ✅ Test: Tests node calls run_tests
- ✅ Test: No execute_command calls remain
- ✅ Test: Syntax validation passed
- ✅ Test: Imports successful

---

### Task 3.0.0.8: Add Task Module to Package Exports

🔧 **Priority**: 🟢 Low

📁 **Files**: `build_tools/sharpy_auto_builder/__init__.py`

**Actions**:

1. [x] Import task functions: `execute_claude_cli`, `execute_copilot_cli`, `run_tests`
2. [x] Import data types: `TaskExecutionResult`, `TaskIdempotencyFallback`
3. [x] Add all exports to `__all__` list under "# Tasks" section

**Verification**:
- ✅ Test: Can import from `sharpy_auto_builder` package
- ✅ Test: All task exports accessible
- ✅ Test: Correct types returned

---

### Task 3.0.0.9: Write Task Tests

🔧 **Priority**: 🟢 Low

📁 **Files**: `build_tools/tests/test_tasks.py`

**Actions**:

1. [x] Create `TestTaskExecutionResult` class (4 tests)
2. [x] Create `TestComputeInputHash` class (4 tests)
3. [x] Create `TestIsRateLimited` class (3 tests)
4. [x] Create `TestTaskIdempotencyFallback` class (4 tests)
5. [x] Add documentation about testing @task decorated functions
   - Note: Task functions require LangGraph runnable context
   - Note: Integration tested via orchestrator

**Verification**:
- ✅ Test: `pytest build_tools/tests/test_tasks.py` - 15 tests passed
- ✅ Test: All helper functions covered
- ✅ Test: Data structures fully tested

---

## Phase 4.0.0: Long-Term Memory Store

**Goal:** Add LangGraph memory store to enable cross-task and cross-session learning, allowing the orchestrator to leverage patterns from successful past implementations.

**Priority:** 🟠 Medium
**Estimated Effort:** 4-6 hours
**Risk Level:** Low (additive feature)

⚠️ **Note:** Memory store is independent of checkpointer. Can be implemented in parallel with other phases.

---

### Task 4.0.0.1: Add Memory Dependencies

🔧 **Priority**: 🟠 Medium

📁 **Files**: `build_tools/sharpy_auto_builder/requirements.txt`

**Actions**:

1. [x] Add `langchain-openai>=0.2.0` for OpenAI embeddings (optional)
2. [x] Or add `sentence-transformers>=2.2.0` for local embeddings (optional)
3. [x] Memory works without embeddings (exact key matching)

**Verification**:
- ✅ Test: Dependencies install without error

---

### Task 4.0.0.2: Add Memory Configuration

🔧 **Priority**: 🟠 Medium

📁 **Files**: `build_tools/sharpy_auto_builder/config.py`

**Actions**:

1. [x] Add `MemoryConfig` dataclass with fields:
   - `enabled: bool = True`
   - `embedding_provider: Optional[Literal["openai", "local"]] = None`
   - `openai_embedding_model: str = "text-embedding-3-small"`
   - `local_embedding_model: str = "all-MiniLM-L6-v2"`
   - `max_patterns_per_query: int = 5`
   - `min_similarity_score: float = 0.5`
   - `max_pattern_length: int = 1000`
   - `max_patterns_stored: int = 10000`
2. [x] Add `memory: MemoryConfig` field to main `Config` class
3. [x] Add `memory_store_path` property returning `self.state_dir / "memory_store.db"`

**Verification**:
- ✅ Test: `Config().memory.enabled` returns True

---

### Task 4.0.0.3: Create Memory Management Module

🔧 **Priority**: 🚨 High

📁 **Files**: `build_tools/sharpy_auto_builder/memory.py`

**Actions**:

1. [x] Create new file `memory.py`
2. [x] Add `Pattern` dataclass with fields: `id`, `namespace`, `task_type`, `description`, `solution`, `files`, `tags`, `created_at`, `success`, `metadata`
3. [x] Add `to_dict()` and `from_store_item()` methods
4. [x] Create `MemoryManager` class with namespace constants:
   - `NS_IMPLEMENTATION = ("sharpy", "implementation_patterns")`
   - `NS_ERRORS = ("sharpy", "error_patterns")`
   - `NS_CODEBASE = ("sharpy", "codebase_knowledge")`
   - `NS_SPEC = ("sharpy", "spec_patterns")`

**Verification**:
- ✅ Test: `MemoryManager` instantiates

---

### Task 4.0.0.4: Implement Pattern Storage Methods

🔧 **Priority**: 🚨 High

📁 **Files**: `build_tools/sharpy_auto_builder/memory.py`

**Actions**:

1. [x] Implement `store_implementation_pattern(task_type, description, solution, files, tags, task_id, metadata)` returning pattern ID
2. [x] Implement `store_error_pattern(error_type, description, error_message, solution, files, task_id)` returning pattern ID
3. [x] Implement `store_codebase_knowledge(category, knowledge, source_file, confidence)` returning knowledge ID
4. [x] Truncate long values to `max_pattern_length`
5. [x] Return empty string if memory disabled

**Verification**:
- ✅ Test: Patterns stored in correct namespace

---

### Task 4.0.0.5: Implement Pattern Search Methods

🔧 **Priority**: 🚨 High

📁 **Files**: `build_tools/sharpy_auto_builder/memory.py`

**Actions**:

1. [x] Implement `search_patterns(query, namespace, limit)` returning list of `Pattern`
2. [x] Use `store.search(namespace, query=query, limit=limit)` for semantic search
3. [x] Fallback to listing if search fails
4. [x] Return empty list if memory disabled

**Verification**:
- ✅ Test: Search returns relevant patterns

---

### Task 4.0.0.6: Implement Context Generation Methods

🔧 **Priority**: 🟠 Medium

📁 **Files**: `build_tools/sharpy_auto_builder/memory.py`

**Actions**:

1. [x] Implement `get_implementation_context(task_description)` returning formatted context string
2. [x] Implement `get_error_avoidance_context(task_description)` returning warnings about past errors
3. [x] Implement `get_codebase_context(file_path)` returning relevant codebase knowledge

**Verification**:
- ✅ Test: Context includes relevant patterns

---

### Task 4.0.0.7: Initialize Memory Store in Orchestrator

🔧 **Priority**: 🚨 High

📁 **Files**: `build_tools/sharpy_auto_builder/orchestrator.py`

**Actions**:

1. [x] Add imports: `from langgraph.store.memory import InMemoryStore`, `from .memory import MemoryManager`
2. [x] Add `_create_memory_store()` method returning configured `InMemoryStore`
3. [x] Configure embeddings if provider specified in config
4. [x] Create `self.memory_store` and `self.memory_manager` in `__init__`
5. [x] Update graph compilation: `self.app = self.graph.compile(checkpointer=..., store=self.memory_store)`

**Verification**:
- ✅ Test: Memory store initialized
- ✅ Test: Works with embeddings disabled

---

### Task 4.0.0.8: Inject Memory Context into Prompts

🔧 **Priority**: 🟠 Medium

📁 **Files**: `build_tools/sharpy_auto_builder/orchestrator.py`

**Actions**:

1. [ ] Update `_build_implementation_prompt(task_data, state)` to include memory context
2. [ ] Call `memory_manager.get_implementation_context(task_description)`
3. [ ] Call `memory_manager.get_error_avoidance_context(task_description)`
4. [ ] Append context to prompt parts

**Verification**:
- ✅ Test: Prompts include relevant past patterns

---

### Task 4.0.0.9: Store Patterns After Task Completion

🔧 **Priority**: 🟠 Medium

📁 **Files**: `build_tools/sharpy_auto_builder/orchestrator.py`

**Actions**:

1. [ ] Update `_update_ground_truth_node` to store successful patterns
2. [ ] On success: call `memory_manager.store_implementation_pattern()`
3. [ ] On failure with error: call `memory_manager.store_error_pattern()`
4. [ ] Add `_extract_solution_summary(result)` helper to extract key solution parts
5. [ ] Add `_categorize_error(error_message)` helper returning error type
6. [ ] Wrap in try/except to not fail task on memory errors

**Verification**:
- ✅ Test: Successful tasks create patterns
- ✅ Test: Failed tasks create error patterns

---

### Task 4.0.0.10: Add Memory CLI Commands

🔧 **Priority**: 🟢 Low

📁 **Files**: `build_tools/sharpy_auto_builder/cli.py`

**Actions**:

1. [ ] Add `memory` command group
2. [ ] Add `memory search --namespace -n --limit -l QUERY` command
3. [ ] Add `memory stats --namespace -n` command
4. [ ] Add `memory clear --namespace -n --confirm` command

**Verification**:
- ✅ Test: `./auto_builder.sh memory search "parsing"` returns results
- ✅ Test: `./auto_builder.sh memory stats` shows counts

---

### Task 4.0.0.11: Write Memory Tests

🔧 **Priority**: 🟢 Low

📁 **Files**: `build_tools/tests/test_memory.py`

**Actions**:

1. [ ] Create test file with `TestMemoryManager` class
2. [ ] Test storing implementation patterns
3. [ ] Test storing error patterns
4. [ ] Test searching patterns
5. [ ] Test context generation
6. [ ] Test disabled memory mode
7. [ ] Add `TestPattern` class for dataclass tests
8. [ ] Add `TestMemoryIntegration` class

**Verification**:
- ✅ Run: `pytest build_tools/tests/test_memory.py`

---

## Verification Checklist

After completing all phases, verify:

### Phase 1: Durable Persistence
- [ ] `langgraph-checkpoint-sqlite` installed
- [ ] `orchestrator_checkpoints.db` created on first run
- [ ] `--thread-id` resumes sessions
- [ ] Rate limit shows resume instructions
- [ ] `--list-sessions` shows previous sessions
- [ ] Checkpoint cleanup runs automatically
- [ ] All tests pass

### Phase 2: Native Interrupts
- [ ] `interrupt()` used in human review node
- [ ] `wait_for_human` node removed
- [ ] CLI handler displays payloads correctly
- [ ] Resume with `Command(resume=...)` works
- [ ] Input validation re-prompts on invalid input
- [ ] Deprecation warning on `HumanLoopManager`
- [ ] All tests pass

### Phase 3: Idempotent Tasks
- [ ] `tasks.py` module created
- [ ] Tasks use `@task` decorator
- [ ] Fallback idempotency tracking works
- [ ] Orchestrator uses task functions
- [ ] On replay, cached results used
- [ ] All tests pass

### Phase 4: Memory Store
- [ ] `memory.py` module created
- [ ] Memory store initialized in orchestrator
- [ ] Patterns stored after tasks
- [ ] Memory context in prompts
- [ ] CLI memory commands work
- [ ] Disabled mode works without errors
- [ ] All tests pass

---

## Cross-References

| Document | Description |
|----------|-------------|
| `implementation_plan_1_durable_persistence.md` | Detailed Plan 1 |
| `implementation_plan_2_native_interrupts.md` | Detailed Plan 2 |
| `implementation_plan_3_idempotent_tasks.md` | Detailed Plan 3 |
| `implementation_plan_4_memory_store.md` | Detailed Plan 4 |
| `implementation_plan_amendments.md` | Amendments and best practices |
| [LangGraph Persistence](https://docs.langchain.com/oss/python/langgraph/persistence) | Official docs |
| [LangGraph Interrupts](https://docs.langchain.com/oss/python/langgraph/interrupts) | Official docs |
| [LangGraph Durable Execution](https://docs.langchain.com/oss/python/langgraph/durable-execution) | Official docs |
| [LangGraph Memory](https://docs.langchain.com/oss/python/langgraph/add-memory) | Official docs |
