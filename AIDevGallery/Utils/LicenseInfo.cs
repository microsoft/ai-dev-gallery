// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace AIDevGallery.Utils;

internal class LicenseInfo
{
    public static LicenseInfo GetLicenseInfo(string? license)
    {
        if (string.IsNullOrEmpty(license) || !Licenses.TryGetValue(license, out var licenseInfo))
        {
            return Licenses["unknown"];
        }

        return licenseInfo;
    }

    private static Dictionary<string, LicenseInfo> Licenses { get; } = new Dictionary<string, LicenseInfo>
        {
            {
                "apache-2.0", new LicenseInfo
                {
                    Name = "Apache license 2.0",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/apache-2.0.md"
                }
            },
            {
                "mit", new LicenseInfo
                {
                    Name = "MIT",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/mit.md"
                }
            },
            {
                "openrail", new LicenseInfo
                {
                    Name = "OpenRAIL license family",
                    LicenseUrl = "https://huggingface.co/blog/open_rail"
                }
            },
            {
                "bigscience-openrail-m", new LicenseInfo
                {
                    Name = "BigScience OpenRAIL-M",
                    LicenseUrl = "https://bigscience.huggingface.co/blog/bigscience-openrail-m"
                }
            },
            {
                "creativeml-openrail-m", new LicenseInfo
                {
                    Name = "CreativeML OpenRAIL-M",
                    LicenseUrl = "https://huggingface.co/spaces/CompVis/stable-diffusion-license"
                }
            },
            {
                "bigscience-bloom-rail-1.0", new LicenseInfo
                {
                    Name = "BigScience BLOOM RAIL 1.0",
                    LicenseUrl = "https://huggingface.co/spaces/bigscience/license"
                }
            },
            {
                "bigcode-openrail-m", new LicenseInfo
                {
                    Name = "BigCode Open RAIL-M v1",
                    LicenseUrl = "https://www.bigcode-project.org/docs/pages/bigcode-openrail"
                }
            },
            {
                "afl-3.0", new LicenseInfo
                {
                    Name = "Academic Free License v3.0",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/afl-3.0.md"
                }
            },
            {
                "artistic-2.0", new LicenseInfo
                {
                    Name = "Artistic license 2.0",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/artistic-2.0.md"
                }
            },
            {
                "bsl-1.0", new LicenseInfo
                {
                    Name = "Boost Software License 1.0",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/bsl-1.0.md"
                }
            },
            {
                "bsd", new LicenseInfo
                {
                    Name = "BSD license family"
                }
            },
            {
                "bsd-2-clause", new LicenseInfo
                {
                    Name = "BSD 2-clause \"Simplified\" license",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/bsd-2-clause.md"
                }
            },
            {
                "bsd-3-clause", new LicenseInfo
                {
                    Name = "BSD 3-clause \"New\" or \"Revised\" license",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/bsd-3-clause.md"
                }
            },
            {
                "bsd-3-clause-clear", new LicenseInfo
                {
                    Name = "BSD 3-clause Clear license",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/bsd-3-clause-clear.md"
                }
            },
            {
                "c-uda", new LicenseInfo
                {
                    Name = "Computational Use of Data Agreement",
                    LicenseUrl = "https://spdx.org/licenses/C-UDA-1.0"
                }
            },
            {
                "cc", new LicenseInfo
                {
                    Name = "Creative Commons license family"
                }
            },
            {
                "cc0-1.0", new LicenseInfo
                {
                    Name = "Creative Commons Zero v1.0 Universal",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/cc0-1.0.md"
                }
            },
            {
                "cc-by-2.0", new LicenseInfo
                {
                    Name = "Creative Commons Attribution 2.0",
                    LicenseUrl = "https://spdx.org/licenses/CC-BY-2.0"
                }
            },
            {
                "cc-by-2.5", new LicenseInfo
                {
                    Name = "Creative Commons Attribution 2.5",
                    LicenseUrl = "https://spdx.org/licenses/CC-BY-2.5"
                }
            },
            {
                "cc-by-3.0", new LicenseInfo
                {
                    Name = "Creative Commons Attribution 3.0",
                    LicenseUrl = "https://spdx.org/licenses/CC-BY-3.0"
                }
            },
            {
                "cc-by-4.0", new LicenseInfo
                {
                    Name = "Creative Commons Attribution 4.0",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/cc-by-4.0.md"
                }
            },
            {
                "cc-by-sa-3.0", new LicenseInfo
                {
                    Name = "Creative Commons Attribution Share Alike 3.0",
                    LicenseUrl = "https://spdx.org/licenses/CC-BY-SA-3.0"
                }
            },
            {
                "cc-by-sa-4.0", new LicenseInfo
                {
                    Name = "Creative Commons Attribution Share Alike 4.0",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/cc-by-sa-4.0.md"
                }
            },
            {
                "cc-by-nc-2.0", new LicenseInfo
                {
                    Name = "Creative Commons Attribution Non Commercial 2.0",
                    LicenseUrl = "https://spdx.org/licenses/CC-BY-NC-2.0"
                }
            },
            {
                "cc-by-nc-3.0", new LicenseInfo
                {
                    Name = "Creative Commons Attribution Non Commercial 3.0",
                    LicenseUrl = "https://spdx.org/licenses/CC-BY-NC-3.0"
                }
            },
            {
                "cc-by-nc-4.0", new LicenseInfo
                {
                    Name = "Creative Commons Attribution Non Commercial 4.0",
                    LicenseUrl = "https://spdx.org/licenses/CC-BY-NC-4.0"
                }
            },
            {
                "cc-by-nd-4.0", new LicenseInfo
                {
                    Name = "Creative Commons Attribution No Derivatives 4.0",
                    LicenseUrl = "https://spdx.org/licenses/CC-BY-ND-4.0"
                }
            },
            {
                "cc-by-nc-nd-3.0", new LicenseInfo
                {
                    Name = "Creative Commons Attribution Non Commercial No Derivatives 3.0",
                    LicenseUrl = "https://spdx.org/licenses/CC-BY-NC-ND-3.0"
                }
            },
            {
                "cc-by-nc-nd-4.0", new LicenseInfo
                {
                    Name = "Creative Commons Attribution Non Commercial No Derivatives 4.0",
                    LicenseUrl = "https://spdx.org/licenses/CC-BY-NC-ND-4.0"
                }
            },
            {
                "cc-by-nc-sa-2.0", new LicenseInfo
                {
                    Name = "Creative Commons Attribution Non Commercial Share Alike 2.0",
                    LicenseUrl = "https://spdx.org/licenses/CC-BY-NC-SA-2.0"
                }
            },
            {
                "cc-by-nc-sa-3.0", new LicenseInfo
                {
                    Name = "Creative Commons Attribution Non Commercial Share Alike 3.0",
                    LicenseUrl = "https://spdx.org/licenses/CC-BY-NC-SA-3.0"
                }
            },
            {
                "cc-by-nc-sa-4.0", new LicenseInfo
                {
                    Name = "Creative Commons Attribution Non Commercial Share Alike 4.0",
                    LicenseUrl = "https://spdx.org/licenses/CC-BY-NC-SA-4.0"
                }
            },
            {
                "cdla-sharing-1.0", new LicenseInfo
                {
                    Name = "Community Data License Agreement – Sharing, Version 1.0",
                    LicenseUrl = "https://spdx.org/licenses/CDLA-SHARING-1.0"
                }
            },
            {
                "cdla-permissive-1.0", new LicenseInfo
                {
                    Name = "Community Data License Agreement – Permissive, Version 1.0",
                    LicenseUrl = "https://spdx.org/licenses/CDLA-Permissive-1.0"
                }
            },
            {
                "cdla-permissive-2.0", new LicenseInfo
                {
                    Name = "Community Data License Agreement – Permissive, Version 2.0",
                    LicenseUrl = "https://spdx.org/licenses/CDLA-Permissive-2.0"
                }
            },
            {
                "wtfpl", new LicenseInfo
                {
                    Name = "Do What The F*ck You Want To Public License",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/wtfpl.md"
                }
            },
            {
                "ecl-2.0", new LicenseInfo
                {
                    Name = "Educational Community License v2.0",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/ecl-2.0.md"
                }
            },
            {
                "epl-1.0", new LicenseInfo
                {
                    Name = "Eclipse Public License 1.0",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/epl-1.0.md"
                }
            },
            {
                "epl-2.0", new LicenseInfo
                {
                    Name = "Eclipse Public License 2.0",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/epl-2.0.md"
                }
            },
            {
                "etalab-2.0", new LicenseInfo
                {
                    Name = "Etalab Open License 2.0",
                    LicenseUrl = "https://spdx.org/licenses/etalab-2.0"
                }
            },
            {
                "eupl-1.1", new LicenseInfo
                {
                    Name = "European Union Public License 1.1",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/eupl-1.1.md"
                }
            },
            {
                "agpl-3.0", new LicenseInfo
                {
                    Name = "GNU Affero General Public License v3.0",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/agpl-3.0.md"
                }
            },
            {
                "gfdl", new LicenseInfo
                {
                    Name = "GNU Free Documentation License family"
                }
            },
            {
                "gpl", new LicenseInfo
                {
                    Name = "GNU General Public License family"
                }
            },
            {
                "gpl-2.0", new LicenseInfo
                {
                    Name = "GNU General Public License v2.0",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/gpl-2.0.md"
                }
            },
            {
                "gpl-3.0", new LicenseInfo
                {
                    Name = "GNU General Public License v3.0",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/gpl-3.0.md"
                }
            },
            {
                "lgpl", new LicenseInfo
                {
                    Name = "GNU Lesser General Public License family"
                }
            },
            {
                "lgpl-2.1", new LicenseInfo
                {
                    Name = "GNU Lesser General Public License v2.1",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/lgpl-2.1.md"
                }
            },
            {
                "lgpl-3.0", new LicenseInfo
                {
                    Name = "GNU Lesser General Public License v3.0",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/lgpl-3.0.md"
                }
            },
            {
                "isc", new LicenseInfo
                {
                    Name = "ISC",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/isc.md"
                }
            },
            {
                "lppl-1.3c", new LicenseInfo
                {
                    Name = "LaTeX Project Public License v1.3c",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/lppl-1.3c.md"
                }
            },
            {
                "ms-pl", new LicenseInfo
                {
                    Name = "Microsoft Public License",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/ms-pl.md"
                }
            },
            {
                "apple-ascl", new LicenseInfo
                {
                    Name = "Apple Sample Code license",
                    LicenseUrl = "https://developer.apple.com/support/downloads/terms/apple-sample-code/Apple-Sample-Code-License.pdf"
                }
            },
            {
                "mpl-2.0", new LicenseInfo
                {
                    Name = "Mozilla Public License 2.0",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/mpl-2.0.md"
                }
            },
            {
                "odc-by", new LicenseInfo
                {
                    Name = "Open Data Commons License Attribution family"
                }
            },
            {
                "odbl", new LicenseInfo
                {
                    Name = "Open Database License family"
                }
            },
            {
                "openrail++", new LicenseInfo
                {
                    Name = "Open Rail++-M License",
                    LicenseUrl = "https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0/blob/main/LICENSE.md"
                }
            },
            {
                "osl-3.0", new LicenseInfo
                {
                    Name = "Open Software License 3.0",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/osl-3.0.md"
                }
            },
            {
                "postgresql", new LicenseInfo
                {
                    Name = "PostgreSQL License",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/postgresql.md"
                }
            },
            {
                "ofl-1.1", new LicenseInfo
                {
                    Name = "SIL Open Font License 1.1",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/ofl-1.1.md"
                }
            },
            {
                "ncsa", new LicenseInfo
                {
                    Name = "University of Illinois/NCSA Open Source License",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/ncsa.md"
                }
            },
            {
                "unlicense", new LicenseInfo
                {
                    Name = "The Unlicense",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/unlicense.md"
                }
            },
            {
                "zlib", new LicenseInfo
                {
                    Name = "zLib License",
                    LicenseUrl = "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/zlib.md"
                }
            },
            {
                "pddl", new LicenseInfo
                {
                    Name = "Open Data Commons Public Domain Dedication and License",
                    LicenseUrl = "https://spdx.org/licenses/PDDL-1.0"
                }
            },
            {
                "lgpl-lr", new LicenseInfo
                {
                    Name = "Lesser General Public License For Linguistic Resources",
                    LicenseUrl = "https://spdx.org/licenses/LGPLLR"
                }
            },
            {
                "deepfloyd-if-license", new LicenseInfo
                {
                    Name = "DeepFloyd IF Research License Agreement"
                }
            },
            {
                "llama2", new LicenseInfo
                {
                    Name = "Llama 2 Community License Agreement",
                    LicenseUrl = "https://huggingface.co/meta-llama/Llama-2-7b-chat-hf/blob/main/LICENSE.txt"
                }
            },
            {
                "llama3", new LicenseInfo
                {
                    Name = "Llama 3 Community License Agreement",
                    LicenseUrl = "https://huggingface.co/meta-llama/Meta-Llama-3-8B/blob/main/LICENSE"
                }
            },
            {
                "llama3.1", new LicenseInfo
                {
                    Name = "Llama 3.1 Community License Agreement",
                    LicenseUrl = "https://huggingface.co/meta-llama/Meta-Llama-3.1-8B/blob/main/LICENSE"
                }
            },
            {
                "llama3.2", new LicenseInfo
                {
                    Name = "Llama 3.2 Community License Agreement",
                    LicenseUrl = "https://huggingface.co/meta-llama/Llama-3.2-1B/blob/main/LICENSE.txt"
                }
            },
            {
                "gemma", new LicenseInfo
                {
                    Name = "Gemma Terms of Use",
                    LicenseUrl = "https://ai.google.dev/gemma/terms"
                }
            },
            {
                "unknown", new LicenseInfo
                {
                    Name = "Unknown"
                }
            },
            {
                "other", new LicenseInfo
                {
                    Name = "Other"
                }
            }
        };

    public required string Name { get; set; }
    public string? LicenseUrl { get; set; }
}