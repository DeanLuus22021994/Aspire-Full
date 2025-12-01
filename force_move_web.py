import os
import shutil

src_dir = os.path.join(os.getcwd(), "Aspire-Full.Web")
dst_dir = os.path.join(os.getcwd(), "Web", "Aspire-Full.Web")

if not os.path.exists(dst_dir):
    os.makedirs(dst_dir)

for item in os.listdir(src_dir):
    s = os.path.join(src_dir, item)
    d = os.path.join(dst_dir, item)
    try:
        if os.path.isdir(s):
            shutil.move(s, d)
        else:
            shutil.move(s, d)
        print(f"Moved {item}")
    except Exception as e:
        print(f"Failed to move {item}: {e}")
