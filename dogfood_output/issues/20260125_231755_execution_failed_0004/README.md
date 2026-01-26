# Issue Report: execution_failed

**Timestamp:** 2026-01-25T23:17:10.144108
**Type:** execution_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** claude

## Generated Sharpy Code

```python
# Main entry point - demonstrates multi-module content management system
from data_models import ContentStatus, UserRole, User
from content_types import Article, Video, ContentManager

def main():
    # Create users with different roles
    viewer: User = User("alice", UserRole.Viewer)
    editor: User = User("bob", UserRole.Editor)
    admin: User = User("carol", UserRole.Admin)
    
    print(f"User {admin.username} can publish: {admin.can_publish()}")
    print(f"User {editor.username} can edit: {editor.can_edit()}")
    
    # Create content items
    article: Article = Article("Python vs Sharpy", "Bob Smith", 1500)
    video: Video = Video("Sharpy Tutorial", 45, "1080p")
    
    print(article.get_summary())
    print(video.get_info())
    
    # Manage content publishing
    manager: ContentManager = ContentManager()
    
    # Admin publishes article
    success: bool = manager.process_content(article, admin)
    print(f"Article published: {success}")
    
    # Editor tries to publish video (should fail)
    success = manager.process_content(video, editor)
    print(f"Video published by editor: {success}")
    
    print(f"Total published: {manager.published_count}")

# EXPECTED OUTPUT:
# User carol can publish: True
# User bob can edit: True
# Python vs Sharpy by Bob Smith (1500 words)
# Sharpy Tutorial - 45min @ 1080p
# Article published: True
# Video published by editor: False
# Total published: 1
```

## Error

```
Compilation failed:
  Semantic error at line 25, column 45: Cannot pass argument of type 'Article' to parameter of type 'Content'
  Semantic error at line 29, column 39: Cannot pass argument of type 'Video' to parameter of type 'Content'

```

## Timing

- Generation: 15.80s
- Execution: 0.86s
