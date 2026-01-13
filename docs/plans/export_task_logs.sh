#!/bin/bash

# Script to export all task logs from auto_builder.sh

TASK_IDS=(
    0.1.0.1
    0.1.0.2
    0.1.0.3
    0.1.0.4
    0.1.0.5
    0.1.0.6
    0.1.0.7
    0.1.0.8
    0.1.1.1
    0.1.1.2
    0.1.1.3
    0.1.1.4
    0.1.1.5
    0.1.1.6
    0.1.1.7
    0.1.2.1
    0.1.2.2
    0.1.2.3
    0.1.2.4
    0.1.2.5
    0.1.2.6
    0.1.3.1
    0.1.3.2
    0.1.3.3
    0.1.3.4
    0.1.3.5
    0.1.3.6
    0.1.4.1
    0.1.4.2
    0.1.4.3
    0.1.4.4
    0.1.4.5
    0.1.4.6
    0.1.5.1
    0.1.5.2
    0.1.5.3
    0.1.5.4
    0.1.5.5
    0.1.5.6
    0.1.5.7
    0.1.5.8
    0.1.5.9
)

echo "Exporting ${#TASK_IDS[@]} task logs..."

for task_id in "${TASK_IDS[@]}"; do
    output_file="task_log_${task_id}.txt"
    echo "Exporting task $task_id to $output_file..."
    ./build_tools/auto_builder.sh logs --show-prompt --task-id "$task_id" > "$output_file" 2>&1
done

echo "Done! Exported ${#TASK_IDS[@]} task logs."
