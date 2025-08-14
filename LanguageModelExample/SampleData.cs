namespace LanguageModelExample
{
    public static class SampleData
    {
        public static class Prompts
        {
            public static readonly string[] BasicPrompts = new[]
            {
                "Tell me a short story about a robot learning to paint",
                "Explain quantum computing in simple terms",
                "Write a haiku about artificial intelligence",
                "Describe the future of renewable energy",
                "Create a recipe for digital cookies"
            };

            public static readonly string[] SummaryTexts = new[]
            {
                @"Artificial Intelligence (AI) has emerged as one of the most transformative technologies of the 21st century. 
                From machine learning algorithms that power recommendation systems to natural language processing that enables 
                chatbots and virtual assistants, AI is reshaping how we live and work. The technology has applications across 
                virtually every industry, from healthcare and finance to transportation and entertainment. Machine learning, 
                a subset of AI, allows computers to learn and improve from experience without being explicitly programmed. 
                Deep learning, which uses neural networks with multiple layers, has achieved remarkable breakthroughs in 
                image recognition, speech processing, and game playing. However, the rapid advancement of AI also raises 
                important questions about ethics, privacy, and the future of employment. As AI systems become more capable, 
                we must carefully consider how to ensure they are developed and deployed responsibly, with appropriate 
                safeguards and oversight mechanisms in place.",

                @"Climate change represents one of the most pressing challenges facing humanity today. The scientific 
                consensus is clear: human activities, particularly the burning of fossil fuels, are driving unprecedented 
                increases in global temperatures. These changes are already manifesting in more frequent and intense 
                extreme weather events, rising sea levels, and shifting precipitation patterns. The consequences extend 
                beyond environmental impacts to include economic disruption, food security challenges, and human health 
                risks. Addressing climate change requires a comprehensive approach that includes reducing greenhouse gas 
                emissions through renewable energy adoption, improving energy efficiency, and developing carbon capture 
                technologies. International cooperation is essential, as climate change is a global problem that affects 
                all nations. The Paris Agreement represents a significant step forward, but much more ambitious action 
                is needed to limit global warming to well below 2 degrees Celsius above pre-industrial levels.",

                @"The Internet of Things (IoT) refers to the network of physical devices, vehicles, buildings, and other 
                objects embedded with sensors, software, and network connectivity that enables them to collect and 
                exchange data. This technology has the potential to revolutionize how we interact with the world around 
                us, creating smart homes, connected cities, and intelligent transportation systems. IoT devices can 
                monitor environmental conditions, track inventory, optimize energy usage, and provide real-time insights 
                for decision-making. However, the proliferation of connected devices also introduces new security and 
                privacy concerns, as each device represents a potential entry point for cyber attacks. Ensuring the 
                security of IoT networks requires robust authentication mechanisms, encryption protocols, and regular 
                security updates. As the technology continues to evolve, we can expect to see even more innovative 
                applications that will further integrate the digital and physical worlds."
            };

            public static readonly string[] RewriteTexts = new[]
            {
                "The weather is very bad today. It's raining a lot and the wind is strong.",
                "I think this movie is really good. The acting is excellent and the story is interesting.",
                "The food at this restaurant is delicious. The service is also very good.",
                "This book is very informative. It contains a lot of useful information.",
                "The meeting was productive. We discussed many important topics and made good decisions."
            };

            public static readonly string[] TableTexts = new[]
            {
                @"The company has three main departments: Engineering with 25 employees and $2.5M budget, 
                Marketing with 15 employees and $1.8M budget, and Sales with 30 employees and $3.2M budget.",

                @"The project timeline includes: Planning phase from January to March with 3 team members, 
                Development phase from April to September with 8 team members, Testing phase from October to 
                November with 4 team members, and Deployment phase in December with 2 team members.",

                @"Product sales for Q1: Laptops sold 150 units at $800 each, Tablets sold 200 units at $400 each, 
                Smartphones sold 300 units at $600 each, and Accessories sold 500 units at $50 each."
            };
        }
    }
} 