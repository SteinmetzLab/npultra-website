# Neuropixels Ultra Data Website

This repository hosts an interactive website that allows users to explore the data from our recent publication **Ultra-high density electrodes improve detection, yield, and cell type specificity of brain recordings** by Zhiwen Ye, Andrew M. Shelton, Jordan R. Shaker, Julien Boussard, Jennifer Colonell, Sahar Manavi, Susu Chen, Charlie Windolf, Cole Hurwitz, Tomoyuki Namima, Federico Pedraja, Shahaf Weiss, Bogdan Raducanu, Torbjørn V. Ness, Gaute T. Einevoll, Gilles Laurent, Nathaniel B. Sawtell, Wyeth Bair, Anitha Pasupathy, Carolina Mora Lopez, Barun Dutta, Liam Paninski, Joshua H. Siegle, Christof Koch, Shawn R. Olsen, Timothy D. Harris, Nicholas A. Steinmetz
bioRxiv 2023.08.23.554527; doi: https://doi.org/10.1101/2023.08.23.554527 

To report issues about the website functionality, please post an issue on this repository or send your concerns to dbirman@uw.edu

## Development notes

To update the current data, replace the `metadata_stripped.csv` file with the new data, making sure that the indexes are matched.

Then copy the new data into the data folder and run `convert2csv.ipynb`. This code combines the location data taken from `clusters.CCF_APDVLR.npy` with the `metadat.csv` into a single output file used in Unity. Copy the file into the `Assets/data/` folder in Unity and run the `Tools > Process CSV` function. Then re-build for WebGL and push the code. Done!
