# -*- coding: utf-8 -*-
"""PyInstaller 入口：安装向导"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from installer.main import main

if __name__ == "__main__":
    main()
