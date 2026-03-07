# Issue Report: output_mismatch

**Timestamp:** 2026-03-07T04:11:52.758428
**Type:** output_mismatch
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
from track import Track
from playlist import Playlist

def main():
    track1 = Track("Song One", "Artist A", 3.5)
    track2 = Track("Song Two", "Artist B", 4.2)
    track3 = Track("Song Three", "Artist A", 2.8)
    
    playlist = Playlist.create_empty("My Favorites")
    playlist.add_track(track1)
    playlist.add_track(track2)
    playlist.add_track(track3)
    
    print(playlist.name)
    print(playlist.track_count)
    print(playlist.total_duration)
    
    artist_a_tracks = playlist.get_tracks_by_artist("Artist A")
    print(len(artist_a_tracks))
    
    print(track1)

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
My Favorites
3
10.5
2
Song One by Artist A [3.5 min]

```

### Actual
```
My Favorites
3
10.5
2
Track
```

## Timing

- Generation: 457.17s
- Execution: 4.66s
