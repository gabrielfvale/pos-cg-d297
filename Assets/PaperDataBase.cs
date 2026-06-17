using UnityEngine;

public class PaperDatabase : MonoBehaviour
{
    public static PaperData[] GetAll()
    {
        return new PaperData[]
        {
            // CONFERENCE PAPERS
            new PaperData {
                title = "Generative AI for Facial Expressions in 3D Game Characters: A Retrieval-Augmented Approach",
                eventName = "2025 IEEE/ACM 9th International Workshop on Games and Software Engineering (GAS)",
                abstractText = "Lorem ipsum dolor sit amet, consectetur adipiscing elit...",
                type = "conference",
                authors = new AuthorData[] {
                    new AuthorData { name = "Jonas D. A. L. Junior", university = "Unifor" },
                    new AuthorData { name = "Guadalupe P. S. Ribeiro", university = "Unifor" },
                    new AuthorData { name = "Rafael F. Pessoa", university = "Unifor" },
                    new AuthorData { name = "Alberto H. T. M. Magalhães", university = "Unifor" },
                    new AuthorData { name = "Maria Andréia F. Rodrigues", university = "Unifor" }
                }
            },
            new PaperData {
                title = "Enhancing Emotional Realism in Games: An Optimized Generative AI Framework for Dynamic 3D Facial Animation",
                eventName = "FSE Companion '25: 33rd ACM International Conference on Foundations of Software Engineering",
                abstractText = "Lorem ipsum dolor sit amet, consectetur adipiscing elit...",
                type = "conference",
                authors = new AuthorData[] {
                    new AuthorData { name = "Jonas D. A. L. Junior.", university = "Unifor" },
                    new AuthorData { name = "Rafael F. Pessoa", university = "Unifor" },
                    new AuthorData { name = "Guadalupe P. S. Ribeiro", university = "Unifor" },
                    new AuthorData { name = "João V. V. Lira", university = "Unifor" },
                    new AuthorData { name = "Maria Andréia F. Rodrigues", university = "Unifor" }
                }
            },

            // DISSERTATION
            new PaperData {
                title = "Um Editor de Árvores de Decisão para Construção de Jogos...",
                eventName = "DISSERTATION DEFENSE // MASTER'S DEGREE — PPGIA, Unifor (2024)",
                abstractText = "Lorem ipsum dolor sit amet, consectetur adipiscing elit...",
                type = "dissertation",
                authors = new AuthorData[] {
                    new AuthorData { name = "Rafael G. Barbosa", university = "Unifor" }
                }
            },

            // THESIS
            new PaperData {
                title = "Uma Nova Metodologia para Renderização Híbrida...",
                eventName = "THESIS DEFENSE // DOCTORATE — PPGIA, Unifor (2017)",
                abstractText = "Lorem ipsum dolor sit amet, consectetur adipiscing elit...",
                type = "thesis",
                authors = new AuthorData[] {
                    new AuthorData { name = "Daniel V. M. Macedo", university = "Unifor" }
                }
            }
        };
    }
}
