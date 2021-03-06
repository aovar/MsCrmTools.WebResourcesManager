﻿// PROJECT : MsCrmTools.WebResourcesManager
// This project was developed by Tanguy Touzard
// CODEPLEX: http://xrmtoolbox.codeplex.com
// BLOG: http://mscrmtools.blogspot.com

using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MsCrmTools.WebResourcesManager.AppCode
{
    /// <summary>
    /// Class that manages action on web resources
    /// in Microsoft Dynamics CRM 2011
    /// </summary>
    internal class WebResourceManager
    {
        #region Variables

        /// <summary>
        /// Xrm Organization service
        /// </summary>
        private readonly IOrganizationService innerService;

        #endregion Variables

        #region Constructor

        /// <summary>
        /// Initializes a new instance of class WebResourceManager
        /// </summary>
        /// <param name="service">Xrm Organization service</param>
        public WebResourceManager(IOrganizationService service)
        {
            innerService = service;
        }

        #endregion Constructor

        #region Methods

        internal static string GetBase64StringFromString(string content)
        {
            byte[] byt = System.Text.Encoding.UTF8.GetBytes(content);
            return Convert.ToBase64String(byt);
        }

        internal static string GetContentFromBase64String(string base64)
        {
            byte[] b = Convert.FromBase64String(base64);
            return System.Text.Encoding.UTF8.GetString(b);
        }

        internal void AddToSolution(List<WebResource> resources, string solutionUniqueName)
        {
            foreach (var resource in resources)
            {
                var request = new AddSolutionComponentRequest
                {
                    AddRequiredComponents = false,
                    ComponentId = resource.Id,
                    ComponentType = SolutionComponentType.WebResource,
                    SolutionUniqueName = solutionUniqueName
                };

                innerService.Execute(request);
            }
        }

        /// <summary>
        /// Creates the provided web resource
        /// </summary>
        /// <param name="webResource">Web resource to create</param>
        internal Guid CreateWebResource(Entity webResource)
        {
            try
            {
                return innerService.Create(webResource);
            }
            catch (Exception error)
            {
                throw new Exception("Error while creating web resource: " + error.Message);
            }
        }

        /// <summary>
        /// Deletes the provided web resource
        /// </summary>
        /// <param name="webResource">Web resource to delete</param>
        internal void DeleteWebResource(Entity webResource)
        {
            try
            {
                innerService.Delete(webResource.LogicalName, webResource.Id);
            }
            catch (Exception error)
            {
                throw new Exception("Error while deleting web resource: " + error.Message);
            }
        }

        internal bool HasDependencies(Guid webresourceId)
        {
            var request = new RetrieveDependenciesForDeleteRequest
            {
                ComponentType = SolutionComponentType.WebResource,
                ObjectId = webresourceId
            };

            var response = (RetrieveDependenciesForDeleteResponse)innerService.Execute(request);
            return response.EntityCollection.Entities.Count != 0;
        }

        internal void PublishWebResources(List<WebResource> resources)
        {
            try
            {
                string idsXml = string.Empty;

                foreach (WebResource resource in resources)
                {
                    idsXml += $"<webresource>{resource.Id:B}</webresource>";
                }

                var pxReq1 = new PublishXmlRequest
                {
                    ParameterXml = $"<importexportxml><webresources>{idsXml}</webresources></importexportxml>"
                };

                innerService.Execute(pxReq1);

                foreach (var resource in resources)
                {
                    resource.ReinitStatus();
                }
            }
            catch (Exception error)
            {
                throw new Exception("Error while publishing web resources: " + error.Message);
            }
        }

        /// <summary>
        /// Retrieves a specific web resource from its unique identifier
        /// </summary>
        /// <param name="webresourceId">Web resource unique identifier</param>
        /// <returns>Web resource</returns>
        internal Entity RetrieveWebResource(Guid webresourceId)
        {
            try
            {
                return innerService.Retrieve("webresource", webresourceId, new ColumnSet(true));
            }
            catch (Exception error)
            {
                throw new Exception("Error while retrieving web resource: " + error.Message);
            }
        }

        /// <summary>
        /// Retrieves a specific web resource from its unique name
        /// </summary>
        /// <param name="name">Web resource unique name</param>
        /// <returns>Web resource</returns>
        internal Entity RetrieveWebResource(string name)
        {
            try
            {
                var qba = new QueryByAttribute("webresource");
                qba.Attributes.Add("name");
                qba.Values.Add(name);
                qba.ColumnSet = new ColumnSet(true);

                EntityCollection collection = innerService.RetrieveMultiple(qba);

                if (collection.Entities.Count > 1)
                {
                    throw new Exception($"there are more than one web resource with name '{name}'");
                }

                return collection.Entities.FirstOrDefault();
            }
            catch (Exception error)
            {
                throw new Exception("Error while retrieving web resource: " + error.Message);
            }
        }

        /// <summary>
        /// Retrieves all web resources that are customizable
        /// </summary>
        /// <returns>List of web resources</returns>
        internal EntityCollection RetrieveWebResources(Guid solutionId, List<int> types, bool hideMicrosoftWebresources = true, bool filterByLcid = false, params int[] lcids)
        {
            try
            {
                if (solutionId == Guid.Empty)
                {
                    var qe = new QueryExpression("webresource")
                    {
                        ColumnSet = new ColumnSet(true),
                        Criteria = new FilterExpression
                        {
                            Filters =
                            {
                                new FilterExpression
                                {
                                    FilterOperator = LogicalOperator.And,
                                    Conditions =
                                    {
                                        new ConditionExpression("ishidden", ConditionOperator.Equal, false),
                                    }
                                },
                                new FilterExpression
                                {
                                    FilterOperator = LogicalOperator.Or,
                                     Conditions =
                                    {
                                        new ConditionExpression("ismanaged", ConditionOperator.Equal, false),
                                        new ConditionExpression("iscustomizable", ConditionOperator.Equal, true),
                                    }
                                }
                            }
                        },
                        Orders = { new OrderExpression("name", OrderType.Ascending) }
                    };

                    if (hideMicrosoftWebresources)
                    {
                        qe.Criteria.Filters.First().Conditions.AddRange(
                            new ConditionExpression("name", ConditionOperator.DoesNotBeginWith, "cc_MscrmControls"),
                            new ConditionExpression("name", ConditionOperator.DoesNotBeginWith, "msdyn_")
                            );
                    }

                    if (!string.IsNullOrEmpty(Options.Instance.ExcludedPrefixes))
                    {
                        var prefixes = Options.Instance.ExcludedPrefixes.Split(',');
                        foreach (var prefix in prefixes)
                        {
                            qe.Criteria.Filters.First().AddCondition("name", ConditionOperator.DoesNotBeginWith, prefix);
                        }
                    }

                    if (filterByLcid && lcids.Length != 0)
                    {
                        var lcidFilter = qe.Criteria.Filters.First().AddFilter(LogicalOperator.Or);
                        lcidFilter.AddCondition("languagecode", ConditionOperator.In, lcids.Select(l => (object)l).ToArray());
                        lcidFilter.AddCondition("languagecode", ConditionOperator.Null);
                    }

                    if (types.Count != 0)
                    {
                        qe.Criteria.Filters.First().Conditions.Add(new ConditionExpression("webresourcetype", ConditionOperator.In, types.ToArray()));
                    }

                    return innerService.RetrieveMultiple(qe);
                }
                else
                {
                    var qba = new QueryByAttribute("solutioncomponent") { ColumnSet = new ColumnSet(true) };
                    qba.Attributes.AddRange("solutionid", "componenttype");
                    qba.Values.AddRange(solutionId, 61);

                    var components = innerService.RetrieveMultiple(qba).Entities;

                    var list =
                        components.Select(component => component.GetAttributeValue<Guid>("objectid").ToString("B"))
                            .ToList();

                    if (list.Count > 0)
                    {
                        var qe = new QueryExpression("webresource")
                        {
                            ColumnSet = new ColumnSet(true),
                            Criteria = new FilterExpression
                            {
                                Filters =
                            {
                                new FilterExpression
                                {
                                    FilterOperator = LogicalOperator.And,
                                    Conditions =
                                    {
                                        new ConditionExpression("ishidden", ConditionOperator.Equal, false),
                                        new ConditionExpression("webresourceid", ConditionOperator.In, list.ToArray()),
                                    }
                                },
                                new FilterExpression
                                {
                                    FilterOperator = LogicalOperator.Or,
                                     Conditions =
                                    {
                                        new ConditionExpression("ismanaged", ConditionOperator.Equal, false),
                                        new ConditionExpression("iscustomizable", ConditionOperator.Equal, true),
                                    }
                                }
                            }
                            },
                            Orders = { new OrderExpression("name", OrderType.Ascending) }
                        };

                        if (types.Count != 0)
                        {
                            qe.Criteria.Filters.First().Conditions.Add(new ConditionExpression("webresourcetype", ConditionOperator.In, types.ToArray()));
                        }

                        if (lcids.Length != 0)
                        {
                            var lcidFilter = qe.Criteria.Filters.First().AddFilter(LogicalOperator.Or);
                            lcidFilter.AddCondition("languagecode", ConditionOperator.In, lcids.Select(l => (object)l).ToArray());
                            lcidFilter.AddCondition("languagecode", ConditionOperator.Null);
                        }

                        return innerService.RetrieveMultiple(qe);
                    }

                    return new EntityCollection();
                }
            }
            catch (Exception error)
            {
                throw new Exception("Error while retrieving web resources: " + error.Message);
            }
        }

        /// <summary>
        /// Updates the provided web resource
        /// </summary>
        /// <param name="wr">Web resource to update</param>
        internal void UpdateWebResource(WebResource wr)
        {
            try
            {
                if (wr.Id == Guid.Empty)
                {
                    Entity existingEntity = RetrieveWebResource(wr.ToString());

                    if (existingEntity == null)
                    {
                        wr.Create(innerService);
                    }
                    else
                    {
                        wr.Id = existingEntity.Id;

                        if (existingEntity.Contains("displayname") && string.IsNullOrEmpty(wr.EntityDisplayName))
                        {
                            wr.EntityDisplayName = existingEntity.GetAttributeValue<string>("displayname");
                        }

                        if (existingEntity.Contains("description") && string.IsNullOrEmpty(wr.EntityDescription))
                        {
                            wr.EntityDescription = existingEntity.GetAttributeValue<string>("description");
                        }

                        if (existingEntity.Contains("dependencyxml") && string.IsNullOrEmpty(wr.EntityDependencyXml))
                        {
                            wr.EntityDependencyXml = existingEntity.GetAttributeValue<string>("dependencyxml");
                        }

                        if (existingEntity.Contains("languagecode") && wr.EntityLanguageCode == 0)
                        {
                            wr.EntityLanguageCode = existingEntity.GetAttributeValue<int>("languagecode");
                        }

                        wr.Update(innerService);
                    }
                }
                else
                {
                    wr.Update(innerService);
                }
            }
            catch (Exception error)
            {
                throw new Exception("Error while updating web resource: " + error.Message);
            }
        }

        #endregion Methods
    }
}